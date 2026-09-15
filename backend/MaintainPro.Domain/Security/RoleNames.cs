namespace MaintainPro.Domain.Security;

public static class RoleNames
{
    public const string Technician = "TECHNICIAN";
    public const string Supervisor = "SUPERVISOR";
    public const string Manager = "MANAGER";
    public const string Admin = "ADMIN";
    public static readonly IReadOnlyList<string> All =
        new[] { Technician, Supervisor, Manager, Admin };
}
