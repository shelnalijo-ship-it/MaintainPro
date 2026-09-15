using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Identity;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Users;

public sealed class UserService(IApplicationDbContext db, IPasswordService passwords,
    ICurrentUser currentUser, IAuditWriter audit, TimeProvider clock)
{
    public async Task<PagedResult<UserDto>> ListAsync(UserListQuery request,
        CancellationToken cancellationToken = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Admin);
        Guard.Page(request.Page, request.PageSize);
        var query = WithRoles().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = Guard.Required(request.Search, "Search").ToLowerInvariant();
            query = query.Where(user => user.EmployeeId.ToLower().Contains(search)
                || user.Email.ToLower().Contains(search) || user.FirstName.ToLower().Contains(search)
                || user.LastName.ToLower().Contains(search));
        }
        if (request.IsActive.HasValue) query = query.Where(user => user.IsActive == request.IsActive.Value);
        if (request.DepartmentId.HasValue) query = query.Where(user => user.DepartmentId == request.DepartmentId.Value);
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var role = NormalizeRole(request.Role);
            query = query.Where(user => user.UserRoles.Any(membership => membership.Role.Name == role));
        }
        var total = await query.CountAsync(cancellationToken);
        var users = await query.OrderBy(user => user.EmployeeId).ThenBy(user => user.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<UserDto>(users.Select(UserDto.FromEntity).ToArray(), request.Page, request.PageSize, total);
    }

    public async Task<UserDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Admin);
        return UserDto.FromEntity(await WithRoles().AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken)
            ?? throw new AppException(404, "User was not found."));
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Admin);
        var employeeId = IdentityValidation.EmployeeId(request.EmployeeId);
        var email = IdentityValidation.Email(request.Email);
        var firstName = Guard.Required(request.FirstName, "First name", 100);
        var lastName = Guard.Required(request.LastName, "Last name", 100);
        var mobile = IdentityValidation.Mobile(request.Mobile);
        IdentityValidation.Password(request.Password);
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await ValidateUniquenessAsync(employeeId, email, null, cancellationToken);
        await ValidateDepartmentAsync(request.DepartmentId, cancellationToken);
        var roles = await ValidateRolesAsync(request.Roles, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var user = new User
        {
            EmployeeId = employeeId, FirstName = firstName, LastName = lastName, Email = email,
            Mobile = mobile, DepartmentId = request.DepartmentId, IsActive = request.IsActive,
            PasswordHash = string.Empty, CreatedAt = now, UpdatedAt = now
        };
        user.PasswordHash = passwords.Hash(user, request.Password);
        foreach (var role in roles)
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, User = user, Role = role });
        db.Users.Add(user);
        audit.Record("user.created", nameof(User), user.Id, newValues: UserDto.FromEntity(user));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UserDto.FromEntity(user);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Admin);
        var employeeId = IdentityValidation.EmployeeId(request.EmployeeId);
        var email = IdentityValidation.Email(request.Email);
        var firstName = Guard.Required(request.FirstName, "First name", 100);
        var lastName = Guard.Required(request.LastName, "Last name", 100);
        var mobile = IdentityValidation.Mobile(request.Mobile);
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var user = await FindAsync(id, cancellationToken);
        await ValidateUniquenessAsync(employeeId, email, id, cancellationToken);
        await ValidateDepartmentAsync(request.DepartmentId, cancellationToken);
        var before = UserDto.FromEntity(user);
        var identityChanged = user.EmployeeId != employeeId || user.Email != email;
        user.EmployeeId = employeeId;
        user.Email = email;
        user.FirstName = firstName;
        user.LastName = lastName;
        user.Mobile = mobile;
        user.DepartmentId = request.DepartmentId;
        if (identityChanged)
            await SessionRevocation.RevokeUserAsync(db, user, currentUser.IpAddress,
                clock.GetUtcNow().UtcDateTime, cancellationToken);
        audit.Record("user.updated", nameof(User), user.Id, before, UserDto.FromEntity(user));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UserDto.FromEntity(user);
    }

    public async Task<UserDto> SetStatusAsync(Guid id, UserStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Admin);
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var user = await FindAsync(id, cancellationToken);
        if (user.IsActive != request.IsActive)
        {
            if (!request.IsActive)
            {
                if (user.Id == currentUser.UserId)
                    throw new AppException(409, "Administrators cannot deactivate their own account.");
                await EnsureAnotherAdminAsync(user, cancellationToken);
            }
            var before = UserDto.FromEntity(user);
            user.IsActive = request.IsActive;
            await SessionRevocation.RevokeUserAsync(db, user, currentUser.IpAddress,
                clock.GetUtcNow().UtcDateTime, cancellationToken);
            audit.Record("user.status_changed", nameof(User), user.Id, before, UserDto.FromEntity(user));
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return UserDto.FromEntity(user);
    }

    public async Task<UserDto> SetRolesAsync(Guid id, UserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Admin);
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var user = await FindAsync(id, cancellationToken);
        var roles = await ValidateRolesAsync(request.Roles, cancellationToken);
        var desiredIds = roles.Select(role => role.Id).ToHashSet();
        var existingIds = user.UserRoles.Select(membership => membership.RoleId).ToHashSet();
        if (!desiredIds.SetEquals(existingIds))
        {
            if (user.UserRoles.Any(membership => membership.Role.Name == RoleNames.Admin)
                && roles.All(role => role.Name != RoleNames.Admin))
            {
                if (user.Id == currentUser.UserId)
                    throw new AppException(409, "Administrators cannot remove their own ADMIN role.");
                await EnsureAnotherAdminAsync(user, cancellationToken);
            }
            var before = UserDto.FromEntity(user);
            foreach (var membership in user.UserRoles.Where(membership => !desiredIds.Contains(membership.RoleId)).ToArray())
            {
                db.UserRoles.Remove(membership);
                user.UserRoles.Remove(membership);
            }
            foreach (var role in roles.Where(role => !existingIds.Contains(role.Id)))
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, User = user, Role = role });
            await SessionRevocation.RevokeUserAsync(db, user, currentUser.IpAddress,
                clock.GetUtcNow().UtcDateTime, cancellationToken);
            audit.Record("user.roles_changed", nameof(User), user.Id, before, UserDto.FromEntity(user));
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return UserDto.FromEntity(user);
    }

    public async Task<IReadOnlyList<UserLookupDto>> LookupAsync(string role, CancellationToken cancellationToken = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Manager, RoleNames.Admin);
        var normalizedRole = NormalizeRole(role);
        if (normalizedRole != RoleNames.Technician && normalizedRole != RoleNames.Supervisor)
            throw new AppException(400, "Only technician and supervisor lookups are supported.");
        return await db.Users.AsNoTracking().Where(user => user.IsActive
                && user.UserRoles.Any(membership => membership.Role.Name == normalizedRole))
            .OrderBy(user => user.FirstName).ThenBy(user => user.LastName).ThenBy(user => user.Id)
            .Select(user => new UserLookupDto(user.Id, user.EmployeeId, user.FirstName, user.LastName))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<User> WithRoles() => db.Users.Include(user => user.UserRoles)
        .ThenInclude(membership => membership.Role);

    private async Task<User> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await WithRoles().SingleOrDefaultAsync(user => user.Id == id, cancellationToken)
        ?? throw new AppException(404, "User was not found.");

    private async Task ValidateUniquenessAsync(string employeeId, string email, Guid? exceptId,
        CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(user => user.Id != exceptId && user.EmployeeId == employeeId, cancellationToken))
            throw new AppException(409, "Employee ID is already in use.");
        if (await db.Users.AnyAsync(user => user.Id != exceptId && user.Email == email, cancellationToken))
            throw new AppException(409, "Email is already in use.");
    }

    private async Task ValidateDepartmentAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id.HasValue && !await db.Departments.AnyAsync(department => department.Id == id.Value, cancellationToken))
            throw new AppException(400, "Department was not found.");
    }

    private async Task<List<Role>> ValidateRolesAsync(IReadOnlyList<string>? requestedRoles,
        CancellationToken cancellationToken)
    {
        if (requestedRoles is null || requestedRoles.Count == 0 || requestedRoles.Count > RoleNames.All.Count)
            throw new AppException(400, "Assign at least one of the four standard roles.");
        var names = requestedRoles.Select(NormalizeRole).Distinct().ToArray();
        var roles = await db.Roles.Where(role => names.Contains(role.Name)).ToListAsync(cancellationToken);
        if (roles.Count != names.Length)
            throw new AppException(409, "Standard roles have not been initialized.");
        return roles;
    }

    private static string NormalizeRole(string? role)
    {
        var result = Guard.Required(role, "Role", 50).ToUpperInvariant();
        if (!RoleNames.All.Contains(result)) throw new AppException(400, "Unknown role.");
        return result;
    }

    private async Task EnsureAnotherAdminAsync(User user, CancellationToken cancellationToken)
    {
        if (user.IsActive && user.UserRoles.Any(membership => membership.Role.Name == RoleNames.Admin)
            && !await db.Users.AnyAsync(other => other.Id != user.Id && other.IsActive
                && other.UserRoles.Any(membership => membership.Role.Name == RoleNames.Admin), cancellationToken))
            throw new AppException(409, "At least one active administrator must remain.");
    }
}
