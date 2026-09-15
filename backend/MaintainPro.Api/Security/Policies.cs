using MaintainPro.Domain.Security;

namespace MaintainPro.Api.Security;

public static class Policies
{
    public const string RequireTechnician = nameof(RequireTechnician);
    public const string RequireSupervisor = nameof(RequireSupervisor);
    public const string RequireManager = nameof(RequireManager);
    public const string RequireAdmin = nameof(RequireAdmin);
    public const string ManageUsers = nameof(ManageUsers);
    public const string ManageMachines = nameof(ManageMachines);
    public const string ViewAllMachines = nameof(ViewAllMachines);
    public const string ReadMachines = nameof(ReadMachines);
    public const string ManagePlanning = nameof(ManagePlanning);
    public const string ReadPlanning = nameof(ReadPlanning);
    public const string ReadWorkOrders = nameof(ReadWorkOrders);

    public static void AddPolicies(IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().Build();
            options.AddPolicy(RequireTechnician, policy => policy.RequireRole(RoleNames.Technician));
            options.AddPolicy(RequireSupervisor, policy => policy.RequireRole(RoleNames.Supervisor));
            options.AddPolicy(RequireManager, policy => policy.RequireRole(RoleNames.Manager));
            options.AddPolicy(RequireAdmin, policy => policy.RequireRole(RoleNames.Admin));
            options.AddPolicy(ManageUsers, policy => policy.RequireRole(RoleNames.Admin));
            options.AddPolicy(ManageMachines, policy => policy.RequireRole(RoleNames.Manager, RoleNames.Admin));
            options.AddPolicy(ViewAllMachines, policy => policy.RequireRole(RoleNames.Manager, RoleNames.Admin));
            options.AddPolicy(ReadMachines, policy => policy.RequireRole(RoleNames.All.ToArray()));
            options.AddPolicy(ManagePlanning, policy => policy.RequireRole(RoleNames.Manager, RoleNames.Admin));
            options.AddPolicy(ReadPlanning, policy => policy.RequireRole(RoleNames.Manager, RoleNames.Admin, RoleNames.Supervisor));
            options.AddPolicy(ReadWorkOrders, policy => policy.RequireRole(RoleNames.All.ToArray()));
        });
    }
}
