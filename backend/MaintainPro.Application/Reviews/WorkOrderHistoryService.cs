using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Execution;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Reviews;

public sealed class WorkOrderHistoryService(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<WorkOrderHistoryDto>> HistoryAsync(Guid workOrderId, CancellationToken ct = default)
    {
        await ExecutionAccess.LoadVisibleAsync(db, currentUser, workOrderId,
            tracking: false, includeDefinition: false, ct: ct);
        var visible = ExecutionAccess.VisibleWorkOrders(db, currentUser);
        var events = await db.WorkOrderHistoryEvents.AsNoTracking()
            .Where(x => x.WorkOrderId == workOrderId && visible.Any(order => order.Id == x.WorkOrderId))
            .OrderBy(x => x.SequenceNumber)
            .Select(x => new WorkOrderHistoryDto(x.Id, x.SequenceNumber, x.Action, x.ActorUserId,
                x.ActorName, x.OccurredAt, x.WorkOrderSubmissionId,
                x.Submission == null ? null : x.Submission.VersionNumber, x.Details)).ToListAsync(ct);

        // Jobs generated before execution history was introduced already have an immutable generation
        // audit fact. Preserve it without inventing events from today's mutable lifecycle or assignments.
        var workOrderKey = workOrderId.ToString();
        var existingActions = events.Select(x => x.Action).ToArray();
        var legacy = await db.AuditLogs.AsNoTracking()
            .Where(x => x.EntityType == nameof(WorkOrder) && x.EntityId == workOrderKey &&
                (x.Action == "WorkOrder.Generated" || x.Action == "WorkOrder.Assigned") &&
                !existingActions.Contains(x.Action) && visible.Any(order => order.Id == workOrderId))
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new WorkOrderHistoryDto(x.Id, 0, x.Action, x.UserId, null,
                x.CreatedAt, null, null, "Recorded in the original work-order audit.")).ToListAsync(ct);
        return legacy.Concat(events).ToArray();
    }

    public async Task<PagedResult<MachineMaintenanceHistoryDto>> MachineHistoryAsync(Guid machineId,
        MachineMaintenanceHistoryQuery request, CancellationToken ct = default)
    {
        Guard.Page(request.Page, request.PageSize);
        var actorId = Guard.Authenticated(currentUser);
        var visible = ExecutionAccess.VisibleWorkOrders(db, currentUser);
        var manager = currentUser.Roles.Contains(RoleNames.Manager) || currentUser.Roles.Contains(RoleNames.Admin);
        var technician = currentUser.Roles.Contains(RoleNames.Technician);
        var supervisor = currentUser.Roles.Contains(RoleNames.Supervisor);
        // Current machine assignments allow an empty history view. Historical work-order assignments
        // also retain access, while each returned approved job remains independently scoped.
        var permittedMachine = await db.Machines.AsNoTracking().AnyAsync(machine => machine.Id == machineId &&
            (manager || (technician && machine.MachineOwnerUserId == actorId) ||
                (supervisor && machine.SupervisorUserId == actorId) ||
                visible.Any(order => order.MachineId == machine.Id)), ct);
        if (!permittedMachine) throw new AppException(404, "Machine not found.");
        var query = db.WorkOrderApprovals.AsNoTracking().Where(x =>
            x.Decision == WorkOrderReviewDecision.APPROVED &&
            x.WorkOrder.MachineId == machineId &&
            x.WorkOrder.LifecycleStatus == WorkOrderLifecycleStatus.APPROVED &&
            x.Submission.VersionNumber == x.WorkOrder.SubmissionVersion &&
            visible.Any(order => order.Id == x.WorkOrderId));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.DecisionAt).ThenBy(x => x.WorkOrder.WorkOrderNumber).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new MachineMaintenanceHistoryDto(x.WorkOrderId, x.WorkOrder.WorkOrderNumber,
                x.WorkOrder.Definition.PlanName, x.Submission.SubmittedByUserId,
                x.Submission.TechnicianEmployeeId, x.Submission.TechnicianName,
                x.SupervisorId, x.SupervisorEmployeeId, x.SupervisorName,
                x.WorkOrder.PlannedDate, x.WorkOrder.DueDate, x.Submission.StartedAt,
                x.Submission.CompletedAt, x.Submission.SubmittedAt, x.DecisionAt,
                x.WorkOrder.LifecycleStatus, x.WorkOrderSubmissionId,
                x.Submission.VersionNumber, x.Submission.DurationMinutes)).ToListAsync(ct);
        return new(items, request.Page, request.PageSize, total);
    }
}
