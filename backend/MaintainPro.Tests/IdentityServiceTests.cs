using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using MaintainPro.Application.Identity;
using MaintainPro.Application.Users;
using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class IdentityServiceTests
{
    [Fact]
    public async Task Login_accepts_email_and_employee_id_and_issues_all_assigned_role_claims()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.SeedUserAsync("TECHNICIAN", "SUPERVISOR");

        foreach (var identifier in new[] { user.Email, user.EmployeeId })
        {
            var result = await fixture.Auth.LoginAsync(new LoginRequest(identifier, fixture.Password));
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
            var roles = jwt.Claims.Where(claim => claim.Type is "role" or ClaimTypes.Role)
                .Select(claim => claim.Value).Order().ToArray();

            Assert.Equal(new[] { "SUPERVISOR", "TECHNICIAN" }, roles);
            Assert.Contains(jwt.Claims, claim => claim.Value == user.Id.ToString());
            Assert.Contains(jwt.Claims, claim => claim.Value == user.EmployeeId);
            Assert.Contains(jwt.Claims, claim => claim.Value == user.Email);
            Assert.Equal(user.Id, result.User.Id);
            Assert.True(result.AccessTokenExpiresAt > fixture.Clock.GetUtcNow().UtcDateTime);
            Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
            Assert.DoesNotContain(user.PasswordHash, JsonSerializer.Serialize(result));
        }

        Assert.NotNull(user.LastLoginAt);
        Assert.Equal(2, await fixture.Db.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task Invalid_password_and_unknown_identifier_return_the_same_unauthorized_error()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.SeedUserAsync("TECHNICIAN");

        var wrongPassword = await ModuleFixture.ExpectStatusAsync(401,
            () => fixture.Auth.LoginAsync(new LoginRequest(user.Email, fixture.Password + "wrong")));
        var unknownUser = await ModuleFixture.ExpectStatusAsync(401,
            () => fixture.Auth.LoginAsync(new LoginRequest("missing@example.invalid", fixture.Password)));

        Assert.Equal(wrongPassword.Message, unknownUser.Message);
        Assert.Empty(await fixture.Db.RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task Inactive_users_cannot_login()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.SeedUserAsync("TECHNICIAN");
        user.IsActive = false;
        await fixture.Db.SaveChangesAsync();

        await ModuleFixture.ExpectStatusAsync(401,
            () => fixture.Auth.LoginAsync(new LoginRequest(user.EmployeeId, fixture.Password)));

        Assert.Empty(await fixture.Db.RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task Unknown_and_inactive_logins_still_perform_password_verification()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var inactive = await fixture.SeedUserAsync("TECHNICIAN");
        inactive.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        var spy = new RecordingPasswordService(fixture.Passwords);
        var service = new AuthService(fixture.Db, spy, fixture.Tokens, fixture.Actor, fixture.Audit, fixture.Clock);

        await ModuleFixture.ExpectStatusAsync(401,
            () => service.LoginAsync(new LoginRequest("missing@example.invalid", fixture.Password)));
        await ModuleFixture.ExpectStatusAsync(401,
            () => service.LoginAsync(new LoginRequest(inactive.Email, fixture.Password)));

        Assert.Equal(2, spy.VerifiedUsers.Count);
        Assert.Null(spy.VerifiedUsers[0]);
        Assert.Equal(inactive.Id, spy.VerifiedUsers[1]!.Id);
        Assert.Empty(await fixture.Db.RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task Refresh_rotates_hashes_and_marks_the_predecessor_revoked()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.SeedUserAsync("TECHNICIAN");
        var first = await fixture.Auth.LoginAsync(new LoginRequest(user.Email, fixture.Password));
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));

        var second = await fixture.Auth.RefreshAsync(new RefreshRequest(first.RefreshToken));
        var tokens = await fixture.Db.RefreshTokens.ToListAsync();
        var original = Assert.Single(tokens, token => token.RevokedAt.HasValue);
        var replacement = Assert.Single(tokens, token => !token.RevokedAt.HasValue);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(replacement.Id, original.ReplacedByTokenId);
        Assert.Equal(original.UserId, replacement.UserId);
        Assert.All(tokens, token =>
        {
            Assert.NotEqual(first.RefreshToken, token.TokenHash);
            Assert.NotEqual(second.RefreshToken, token.TokenHash);
        });
    }

    [Fact]
    public async Task Replaying_a_rotated_token_revokes_its_replacement()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.SeedUserAsync("TECHNICIAN");
        var first = await fixture.Auth.LoginAsync(new LoginRequest(user.Email, fixture.Password));
        var second = await fixture.Auth.RefreshAsync(new RefreshRequest(first.RefreshToken));

        await ModuleFixture.ExpectStatusAsync(401,
            () => fixture.Auth.RefreshAsync(new RefreshRequest(first.RefreshToken)));
        await ModuleFixture.ExpectStatusAsync(401,
            () => fixture.Auth.RefreshAsync(new RefreshRequest(second.RefreshToken)));

        Assert.All(await fixture.Db.RefreshTokens.ToListAsync(), token => Assert.NotNull(token.RevokedAt));
    }

    [Fact]
    public async Task Logout_revokes_refresh_and_access_session_without_revoking_another_login()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.SeedUserAsync("TECHNICIAN");
        var first = await fixture.Auth.LoginAsync(new LoginRequest(user.Email, fixture.Password));
        var another = await fixture.Auth.LoginAsync(new LoginRequest(user.Email, fixture.Password));

        await fixture.Auth.LogoutAsync(new RefreshRequest(first.RefreshToken));
        await ModuleFixture.ExpectStatusAsync(401,
            () => fixture.Auth.RefreshAsync(new RefreshRequest(first.RefreshToken)));
        var stillValid = await fixture.Auth.RefreshAsync(new RefreshRequest(another.RefreshToken));

        Assert.Equal(user.Id, stillValid.User.Id);
        var firstJwt = new JwtSecurityTokenHandler().ReadJwtToken(first.AccessToken);
        var familyId = Guid.Parse(firstJwt.Claims.Single(claim => claim.Type == "sid").Value);
        var stamp = user.SecurityStamp;
        Assert.False(await fixture.Auth.ValidateAccessSessionAsync(user.Id, stamp, familyId));
    }

    [Fact]
    public async Task Expired_refresh_and_deactivated_users_are_rejected()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.SeedUserAsync("TECHNICIAN");
        var expired = await fixture.Auth.LoginAsync(new LoginRequest(user.Email, fixture.Password));
        fixture.Clock.Advance(TimeSpan.FromDays(8));

        await ModuleFixture.ExpectStatusAsync(401,
            () => fixture.Auth.RefreshAsync(new RefreshRequest(expired.RefreshToken)));

        var inactive = await fixture.Auth.LoginAsync(new LoginRequest(user.Email, fixture.Password));
        user.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        await ModuleFixture.ExpectStatusAsync(401,
            () => fixture.Auth.RefreshAsync(new RefreshRequest(inactive.RefreshToken)));
    }

    [Fact]
    public async Task Password_change_rejects_incorrect_password_then_revokes_sessions_and_rehashes()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.SeedUserAsync("TECHNICIAN");
        var login = await fixture.Auth.LoginAsync(new LoginRequest(user.Email, fixture.Password));
        fixture.ActAs(user);
        var originalHash = user.PasswordHash;
        var originalStamp = user.SecurityStamp;
        var newPassword = ModuleFixture.NewPassword();

        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Auth.ChangePasswordAsync(
            new ChangePasswordRequest(fixture.Password + "wrong", newPassword)));
        await fixture.Auth.ChangePasswordAsync(new ChangePasswordRequest(fixture.Password, newPassword));

        Assert.NotEqual(originalHash, user.PasswordHash);
        Assert.NotEqual(originalStamp, user.SecurityStamp);
        Assert.NotEqual(newPassword, user.PasswordHash);
        await ModuleFixture.ExpectStatusAsync(401,
            () => fixture.Auth.RefreshAsync(new RefreshRequest(login.RefreshToken)));
        await ModuleFixture.ExpectStatusAsync(401,
            () => fixture.Auth.LoginAsync(new LoginRequest(user.Email, fixture.Password)));
        Assert.Equal(user.Id, (await fixture.Auth.LoginAsync(new LoginRequest(user.Email, newPassword))).User.Id);
        await fixture.AssertAuditHasNoSecretsAsync(fixture.Password, newPassword, originalHash,
            user.PasswordHash, login.AccessToken, login.RefreshToken);
    }

    [Fact]
    public async Task Current_user_dto_excludes_password_and_token_storage_fields()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.SeedUserAsync("TECHNICIAN", "SUPERVISOR");
        fixture.ActAs(user);

        var dto = await fixture.Auth.MeAsync();
        var json = JsonSerializer.Serialize(dto);

        Assert.Equal(user.Id, dto.Id);
        Assert.Equal(new[] { "SUPERVISOR", "TECHNICIAN" }, dto.Roles.Order());
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(user.PasswordHash, json);
    }

    private sealed class RecordingPasswordService(IPasswordService inner) : IPasswordService
    {
        public List<User?> VerifiedUsers { get; } = new();
        public string Hash(User user, string password) => inner.Hash(user, password);
        public PasswordCheck Verify(User? user, string password)
        {
            VerifiedUsers.Add(user);
            return inner.Verify(user, password);
        }
    }
}
