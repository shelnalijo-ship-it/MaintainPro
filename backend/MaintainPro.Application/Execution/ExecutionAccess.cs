using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Execution;

public static class ExecutionAccess
{
    public static IQueryable<WorkOrder> VisibleWorkOrders(IApplicationDbContext db, ICurrentUser user)
    {
        var id = Guard.Authenticated(user);
        if (user.Roles.Contains(RoleNames.Manager) || user.Roles.Contains(RoleNames.Admin)) return db.WorkOrders;
        var technician = user.Roles.Contains(RoleNames.Technician);
        var supervisor = user.Roles.Contains(RoleNames.Supervisor);
        return db.WorkOrders.Where(x => (technician && x.AssignedTechnicianId == id) ||
            (supervisor && x.SupervisorId == id));
    }

    public static async Task<WorkOrder> LoadVisibleAsync(IApplicationDbContext db, ICurrentUser user,
        Guid id, bool tracking = true, bool includeDefinition = true, CancellationToken ct = default)
    {
        var query = VisibleWorkOrders(db, user);
        if (!tracking) query = query.AsNoTracking();
        if (includeDefinition) query = query.Include(x => x.Definition).ThenInclude(x => x.Items);
        return await query.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "Work order not found.");
    }

    public static void RequireTechnician(ICurrentUser user, WorkOrder workOrder)
    {
        Guard.RequireRole(user, RoleNames.Technician);
        if (workOrder.AssignedTechnicianId != user.UserId)
            throw new AppException(403, "Only the assigned technician can execute this work order.");
    }

    public static void RequireSupervisor(ICurrentUser user, WorkOrder workOrder)
    {
        Guard.RequireRole(user, RoleNames.Supervisor);
        if (workOrder.SupervisorId != user.UserId)
            throw new AppException(403, "Only the assigned supervisor can review this work order.");
    }
}
