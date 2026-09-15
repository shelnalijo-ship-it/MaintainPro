using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Security;

namespace MaintainPro.Application.Reporting;

public static class ReportingAccess
{
    public static bool IsManagement(ICurrentUser user) =>
        user.Roles.Contains(RoleNames.Manager) || user.Roles.Contains(RoleNames.Admin);

    public static bool CanViewFinancials(ICurrentUser user) => IsManagement(user);

    public static void RequireManagementDashboard(ICurrentUser user) =>
        Guard.RequireRole(user, RoleNames.Manager, RoleNames.Admin);

    public static void RequireSupervisorDashboard(ICurrentUser user) =>
        Guard.RequireRole(user, RoleNames.Supervisor, RoleNames.Manager, RoleNames.Admin);

    public static void RequireTechnicianDashboard(ICurrentUser user) =>
        Guard.RequireRole(user, RoleNames.Technician);

    public static void RequireReportAccess(ICurrentUser user) =>
        Guard.RequireRole(user, RoleNames.Supervisor, RoleNames.Manager, RoleNames.Admin);

    public static IQueryable<Machine> VisibleMachines(IApplicationDbContext db, ICurrentUser user)
    {
        var id = Guard.Authenticated(user);
        if (IsManagement(user)) return db.Machines;
        var technician = user.Roles.Contains(RoleNames.Technician);
        var supervisor = user.Roles.Contains(RoleNames.Supervisor);
        return db.Machines.Where(x => (technician && x.MachineOwnerUserId == id) ||
            (supervisor && x.SupervisorUserId == id));
    }

    public static IQueryable<WorkOrder> VisibleWorkOrders(IApplicationDbContext db, ICurrentUser user)
    {
        var id = Guard.Authenticated(user);
        if (IsManagement(user)) return db.WorkOrders;
        var technician = user.Roles.Contains(RoleNames.Technician);
        var supervisor = user.Roles.Contains(RoleNames.Supervisor);
        return db.WorkOrders.Where(x => (technician && x.AssignedTechnicianId == id) ||
            (supervisor && x.SupervisorId == id));
    }

    public static IQueryable<Breakdown> VisibleBreakdowns(IApplicationDbContext db, ICurrentUser user)
    {
        var id = Guard.Authenticated(user);
        if (IsManagement(user)) return db.Breakdowns;
        var technician = user.Roles.Contains(RoleNames.Technician);
        var supervisor = user.Roles.Contains(RoleNames.Supervisor);
        return db.Breakdowns.Where(x => (technician &&
                (x.AssignedTechnicianId == id || x.ReportedByUserId == id)) ||
            (supervisor && x.SupervisorId == id));
    }

    public static IQueryable<ExternalService> VisibleExternalServices(IApplicationDbContext db,
        ICurrentUser user)
    {
        var machines = VisibleMachines(db, user);
        return db.ExternalServices.Where(x => machines.Any(machine => machine.Id == x.MachineId));
    }

    public static IQueryable<Machine> ApplyMachineFilters(IQueryable<Machine> query,
        Guid? departmentId, Guid? locationId)
    {
        if (departmentId.HasValue) query = query.Where(x => x.DepartmentId == departmentId);
        if (locationId.HasValue) query = query.Where(x => x.LocationId == locationId);
        return query;
    }
}
