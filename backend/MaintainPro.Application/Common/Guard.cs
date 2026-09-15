using MaintainPro.Application.Abstractions;

namespace MaintainPro.Application.Common;

public static class Guard
{
    public static string Required(string? value, string name, int max = 200)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw new AppException(400, $"{name} is required and must not exceed {max} characters.");
        return value.Trim();
    }

    public static void Page(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > 1_000_000)
            throw new AppException(400, "Page must be positive and pageSize must be between 1 and 100.");
    }

    public static Guid Authenticated(ICurrentUser user) =>
        user.UserId ?? throw new AppException(401, "Authentication is required.");

    public static void RequireRole(ICurrentUser user, params string[] roles)
    {
        Authenticated(user);
        if (!roles.Any(role => user.Roles.Contains(role)))
            throw new AppException(403, "You do not have permission to perform this operation.");
    }
}
