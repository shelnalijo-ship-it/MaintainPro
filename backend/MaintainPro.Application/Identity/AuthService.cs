using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Users;
using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Identity;

public sealed class AuthService(IApplicationDbContext db, IPasswordService passwords,
    ITokenService tokens, ICurrentUser currentUser, IAuditWriter audit, TimeProvider clock)
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var identifier = Guard.Required(request.Identifier, "Email or employee ID", 254);
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 128)
            throw InvalidCredentials();
        var email = identifier.ToLowerInvariant();
        var employeeId = identifier.ToUpperInvariant();
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var user = await db.Users.Include(user => user.UserRoles).ThenInclude(membership => membership.Role)
            .SingleOrDefaultAsync(user => user.Email == email || user.EmployeeId == employeeId, cancellationToken);
        var passwordCheck = passwords.Verify(user, request.Password);
        if (user is null || !user.IsActive || !passwordCheck.Succeeded) throw InvalidCredentials();

        var familyId = Guid.NewGuid();
        var access = tokens.CreateAccessToken(user, familyId);
        var refresh = tokens.CreateRefreshToken();
        var now = clock.GetUtcNow().UtcDateTime;
        if (passwordCheck.NeedsRehash) user.PasswordHash = passwords.Hash(user, request.Password);
        user.LastLoginAt = now;
        db.RefreshTokens.Add(NewSession(user, familyId, refresh, now));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AuthResponse(access.Value, refresh.Value, access.ExpiresAt, UserDto.FromEntity(user));
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashInput(request.RefreshToken);
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var previous = await db.RefreshTokens.Include(token => token.User)
            .ThenInclude(user => user.UserRoles).ThenInclude(membership => membership.Role)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
        if (previous is null) throw InvalidRefresh();
        var now = clock.GetUtcNow().UtcDateTime;

        if (previous.RevokedAt.HasValue || previous.ReplacedByTokenId.HasValue || !previous.User.IsActive)
        {
            // A reused token invalidates its entire session, including its rotated successor.
            await RevokeFamilyAsync(previous.FamilyId, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw InvalidRefresh();
        }
        if (previous.ExpiresAt <= now) throw InvalidRefresh();

        var access = tokens.CreateAccessToken(previous.User, previous.FamilyId);
        var refresh = tokens.CreateRefreshToken();
        var replacement = NewSession(previous.User, previous.FamilyId, refresh, now);
        db.RefreshTokens.Add(replacement);
        previous.RevokedAt = now;
        previous.RevokedByIp = currentUser.IpAddress;
        previous.ReplacedByTokenId = replacement.Id;
        previous.ReplacedByToken = replacement;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AuthResponse(access.Value, refresh.Value, access.ExpiresAt, UserDto.FromEntity(previous.User));
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashInput(request.RefreshToken);
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
        if (token is not null)
        {
            if (currentUser.UserId.HasValue && token.UserId != currentUser.UserId.Value)
                throw InvalidRefresh();
            await RevokeFamilyAsync(token.FamilyId, clock.GetUtcNow().UtcDateTime, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<UserDto> MeAsync(CancellationToken cancellationToken = default)
    {
        var userId = Guard.Authenticated(currentUser);
        var user = await db.Users.AsNoTracking().Include(user => user.UserRoles)
            .ThenInclude(membership => membership.Role)
            .SingleOrDefaultAsync(user => user.Id == userId && user.IsActive, cancellationToken)
            ?? throw new AppException(401, "The user session is no longer valid.");
        return UserDto.FromEntity(user);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var userId = Guard.Authenticated(currentUser);
        IdentityValidation.Password(request.NewPassword);
        if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 128)
            throw new AppException(400, "The current password is incorrect.");
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(user => user.Id == userId && user.IsActive, cancellationToken)
            ?? throw new AppException(401, "The user session is no longer valid.");
        if (!passwords.Verify(user, request.CurrentPassword).Succeeded)
            throw new AppException(400, "The current password is incorrect.");
        if (passwords.Verify(user, request.NewPassword).Succeeded)
            throw new AppException(400, "The new password must differ from the current password.");

        user.PasswordHash = passwords.Hash(user, request.NewPassword);
        await SessionRevocation.RevokeUserAsync(db, user, currentUser.IpAddress,
            clock.GetUtcNow().UtcDateTime, cancellationToken);
        audit.Record("user.password_changed", nameof(User), user.Id);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> ValidateAccessSessionAsync(Guid userId, Guid securityStamp, Guid familyId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return await db.RefreshTokens.AsNoTracking().AnyAsync(token => token.UserId == userId
            && token.FamilyId == familyId && token.RevokedAt == null && token.ExpiresAt > now
            && token.User.IsActive && token.User.SecurityStamp == securityStamp, cancellationToken);
    }

    private RefreshToken NewSession(User user, Guid familyId, RefreshTokenValue refresh, DateTime now) => new()
    {
        UserId = user.Id, User = user, FamilyId = familyId, TokenHash = refresh.Hash,
        CreatedAt = now, ExpiresAt = refresh.ExpiresAt, CreatedByIp = currentUser.IpAddress
    };

    private string HashInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512) throw InvalidRefresh();
        return tokens.HashRefreshToken(value);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken)
    {
        var sessions = await db.RefreshTokens.Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.RevokedAt = now;
            session.RevokedByIp = currentUser.IpAddress;
        }
    }

    private static AppException InvalidCredentials() => new(401, "Invalid credentials.");
    private static AppException InvalidRefresh() => new(401, "The refresh token is invalid or expired.");
}
