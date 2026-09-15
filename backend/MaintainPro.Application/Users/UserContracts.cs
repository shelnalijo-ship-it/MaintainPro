using MaintainPro.Domain.Entities;

namespace MaintainPro.Application.Users;

public sealed record UserDto(Guid Id, string EmployeeId, string FirstName, string LastName,
    string Email, string? Mobile, Guid? DepartmentId, bool IsActive,
    IReadOnlyList<string> Roles, DateTime CreatedAt, DateTime UpdatedAt, DateTime? LastLoginAt)
{
    public static UserDto FromEntity(User user) => new(user.Id, user.EmployeeId,
        user.FirstName, user.LastName, user.Email, user.Mobile, user.DepartmentId,
        user.IsActive, user.UserRoles.Select(membership => membership.Role.Name)
            .OrderBy(name => name).ToArray(), user.CreatedAt, user.UpdatedAt, user.LastLoginAt);
}

public sealed record CreateUserRequest(string EmployeeId, string FirstName, string LastName,
    string Email, string Password, IReadOnlyList<string> Roles, string? Mobile = null,
    Guid? DepartmentId = null, bool IsActive = true);
public sealed record UpdateUserRequest(string EmployeeId, string FirstName, string LastName,
    string Email, string? Mobile = null, Guid? DepartmentId = null);
public sealed record UserStatusRequest(bool IsActive);
public sealed record UserRolesRequest(IReadOnlyList<string> Roles);
public sealed record UserListQuery(string? Search = null, int Page = 1, int PageSize = 20,
    bool? IsActive = null, Guid? DepartmentId = null, string? Role = null);
public sealed record UserLookupDto(Guid Id, string EmployeeId, string FirstName, string LastName);
