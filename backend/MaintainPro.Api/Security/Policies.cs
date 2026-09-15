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
    public const string ManageNotifications = nameof(ManageNotifications);
    public const string ReadEscalations = nameof(ReadEscalations);
    public const string ReadBreakdowns = nameof(ReadBreakdowns);
    public const string ManageBreakdowns = nameof(ManageBreakdowns);
    public const string ReadCalibrations = nameof(ReadCalibrations);
    public const string ManageCalibrations = nameof(ManageCalibrations);
    public const string ReadExternalServices = nameof(ReadExternalServices);
    public const string ManageExternalServiceFollowUps = nameof(ManageExternalServiceFollowUps);
    public const string ManageMachineDocuments = nameof(ManageMachineDocuments);
    public const string ViewManagerDashboard = nameof(ViewManagerDashboard);
    public const string ViewSupervisorDashboard = nameof(ViewSupervisorDashboard);
    public const string ViewTechnicianDashboard = nameof(ViewTechnicianDashboard);
    public const string ViewReports = nameof(ViewReports);
    public const string ViewMachineHistoryReports = nameof(ViewMachineHistoryReports);

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
            options.AddPolicy(ManageNotifications, policy => policy.RequireRole(RoleNames.Manager, RoleNames.Admin));
            options.AddPolicy(ReadEscalations, policy => policy.RequireRole(RoleNames.Supervisor, RoleNames.Manager, RoleNames.Admin));
            options.AddPolicy(ReadBreakdowns, policy => policy.RequireRole(RoleNames.All.ToArray()));
            options.AddPolicy(ManageBreakdowns, policy => policy.RequireRole(RoleNames.Supervisor, RoleNames.Manager, RoleNames.Admin));
            options.AddPolicy(ReadCalibrations, policy => policy.RequireRole(RoleNames.All.ToArray()));
            options.AddPolicy(ManageCalibrations, policy => policy.RequireRole(RoleNames.Manager, RoleNames.Admin));
            options.AddPolicy(ReadExternalServices, policy => policy.RequireRole(RoleNames.All.ToArray()));
            options.AddPolicy(ManageExternalServiceFollowUps,
                policy => policy.RequireRole(RoleNames.Supervisor, RoleNames.Manager, RoleNames.Admin));
            options.AddPolicy(ManageMachineDocuments,
                policy => policy.RequireRole(RoleNames.Supervisor, RoleNames.Manager, RoleNames.Admin));
            options.AddPolicy(ViewManagerDashboard,
                policy => policy.RequireRole(RoleNames.Manager, RoleNames.Admin));
            options.AddPolicy(ViewSupervisorDashboard,
                policy => policy.RequireRole(RoleNames.Supervisor, RoleNames.Manager, RoleNames.Admin));
            options.AddPolicy(ViewTechnicianDashboard,
                policy => policy.RequireRole(RoleNames.Technician));
            options.AddPolicy(ViewReports,
                policy => policy.RequireRole(RoleNames.Supervisor, RoleNames.Manager, RoleNames.Admin));
            options.AddPolicy(ViewMachineHistoryReports,
                policy => policy.RequireRole(RoleNames.All.ToArray()));
        });
    }
}
