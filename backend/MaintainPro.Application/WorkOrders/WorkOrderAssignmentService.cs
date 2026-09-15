using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Execution;
using MaintainPro.Application.Notifications;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.WorkOrders;

public sealed record WorkOrderAssignmentRequest(Guid TechnicianId, string? Reason = null);

public sealed class WorkOrderAssignmentService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock)
{
    public async Task<WorkOrderSummaryDto> ChangeAsync(Guid id, WorkOrderAssignmentRequest request, CancellationToken ct = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Manager, RoleNames.Admin);
        var reason = ExecutionRules.Optional(request.Reason, "Reason", 2000);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var order = await ExecutionAccess.LoadVisibleAsync(db, currentUser, id, ct: ct);
        if (order.LifecycleStatus is not (WorkOrderLifecycleStatus.PLANNED or WorkOrderLifecycleStatus.ASSIGNED)
            || order.StartedAt.HasValue || order.SubmittedAt.HasValue)
            throw new AppException(409, "Assignment may change only before execution starts.");
        var technician = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TechnicianId
            && x.IsActive && x.UserRoles.Any(r => r.Role.Name == RoleNames.Technician), ct)
            ?? throw new AppException(400, "An active user with the TECHNICIAN role is required.");
        if (order.AssignedTechnicianId != technician.Id)
        {
            var previous = order.AssignedTechnicianId;
            order.AssignedTechnicianId = technician.Id;
            order.CurrentTechnicianName = $"{technician.FirstName} {technician.LastName}".Trim();
            order.CurrentTechnicianEmployeeId = technician.EmployeeId;
            order.AssignmentVersion = checked(order.AssignmentVersion + 1);
            order.LifecycleStatus = WorkOrderLifecycleStatus.ASSIGNED;
            var action = previous.HasValue ? "WorkOrder.Reassigned" : "WorkOrder.Assigned";
            ExecutionHistory.Record(db, order, currentUser, clock, action,
                details: $"Assigned to {order.CurrentTechnicianName} ({technician.EmployeeId}). {reason}".Trim());
            audit.Record(action, nameof(WorkOrder), id, new { TechnicianId = previous },
                new { TechnicianId = technician.Id, order.AssignmentVersion, Reason = reason });
            await WorkflowNotificationHooks.AssignedAsync(db, order, currentUser, audit, clock,
                reassigned: previous.HasValue, ct: ct);
            await db.SaveChangesAsync(ct);
        }
        var result = (await new WorkOrderService(db, currentUser, clock).GetAsync(id, ct)).WorkOrder;
        await transaction.CommitAsync(ct);
        return result;
    }
}
