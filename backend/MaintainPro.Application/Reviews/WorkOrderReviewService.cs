using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Execution;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Reviews;

public sealed class WorkOrderReviewService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock)
{
    public Task<WorkOrderReviewDto> ApproveAsync(Guid workOrderId, WorkOrderReviewRequest request,
        CancellationToken ct = default) => DecideAsync(workOrderId, request, WorkOrderReviewDecision.APPROVED, ct);

    public Task<WorkOrderReviewDto> RejectAsync(Guid workOrderId, WorkOrderReviewRequest request,
        CancellationToken ct = default) => DecideAsync(workOrderId, request, WorkOrderReviewDecision.REJECTED, ct);

    public async Task<PagedResult<PendingApprovalDto>> PendingAsync(PendingApprovalQuery request, CancellationToken ct = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Supervisor, RoleNames.Manager, RoleNames.Admin);
        Guard.Page(request.Page, request.PageSize);
        if (request.Priority.HasValue && !Enum.IsDefined(request.Priority.Value))
            throw new AppException(400, "Priority is invalid.");
        if (request.SubmittedFrom.HasValue && request.SubmittedTo.HasValue && request.SubmittedFrom > request.SubmittedTo)
            throw new AppException(400, "SubmittedFrom cannot follow SubmittedTo.");
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var canManage = currentUser.Roles.Contains(RoleNames.Manager) || currentUser.Roles.Contains(RoleNames.Admin);
        var actorId = currentUser.UserId!.Value;
        var query = db.WorkOrderSubmissions.AsNoTracking().Where(x =>
            x.WorkOrder.LifecycleStatus == WorkOrderLifecycleStatus.AWAITING_APPROVAL &&
            x.VersionNumber == x.WorkOrder.SubmissionVersion &&
            (canManage || x.WorkOrder.SupervisorId == actorId) &&
            !db.WorkOrderApprovals.Any(review => review.WorkOrderSubmissionId == x.Id));
        if (request.Priority.HasValue) query = query.Where(x => x.WorkOrder.Priority == request.Priority);
        if (request.SubmittedFrom.HasValue)
        {
            var from = request.SubmittedFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(x => x.SubmittedAt >= from);
        }
        if (request.SubmittedTo.HasValue)
        {
            var to = request.SubmittedTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(x => x.SubmittedAt <= to);
        }
        if (request.Overdue.HasValue)
            query = query.Where(x => (x.WorkOrder.DueDate < today) == request.Overdue.Value);
        if (request.TechnicianId.HasValue) query = query.Where(x => x.SubmittedByUserId == request.TechnicianId);
        if (request.MachineId.HasValue) query = query.Where(x => x.WorkOrder.MachineId == request.MachineId);
        var total = await query.CountAsync(ct);
        // Priority is stored as text: use an explicit numeric rank rather than alphabetical ordering.
        var items = await query.OrderByDescending(x => x.WorkOrder.Priority == MaintenancePriority.CRITICAL ? 3 :
                x.WorkOrder.Priority == MaintenancePriority.HIGH ? 2 : x.WorkOrder.Priority == MaintenancePriority.MEDIUM ? 1 : 0)
            .ThenByDescending(x => x.WorkOrder.DueDate < today).ThenBy(x => x.SubmittedAt).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new PendingApprovalDto(x.WorkOrderId, x.WorkOrder.WorkOrderNumber,
                x.Id, x.VersionNumber, x.WorkOrder.MachineId, x.WorkOrder.Definition.MachineCode,
                x.WorkOrder.Definition.MachineName, x.WorkOrder.Definition.PlanName,
                x.SubmittedByUserId, x.TechnicianName, x.WorkOrder.SupervisorId,
                x.WorkOrder.Priority, x.WorkOrder.PlannedDate, x.WorkOrder.DueDate,
                x.SubmittedAt, x.CompletedAt, x.WorkOrder.DueDate < today)).ToListAsync(ct);
        return new(items, request.Page, request.PageSize, total);
    }

    private async Task<WorkOrderReviewDto> DecideAsync(Guid workOrderId, WorkOrderReviewRequest request,
        WorkOrderReviewDecision decision, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var order = await ExecutionAccess.LoadVisibleAsync(db, currentUser, workOrderId,
            includeDefinition: false, ct: ct);
        ExecutionAccess.RequireSupervisor(currentUser, order);
        var target = decision == WorkOrderReviewDecision.APPROVED
            ? WorkOrderLifecycleStatus.APPROVED : WorkOrderLifecycleStatus.REJECTED;
        ExecutionRules.RequireTransition(order, target);
        var submission = await db.WorkOrderSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.SubmissionId && x.WorkOrderId == workOrderId, ct)
            ?? throw new AppException(404, "Submission not found for this work order.");
        if (submission.SubmittedByUserId == currentUser.UserId)
            throw new AppException(403, "A technician cannot review their own submitted maintenance work.");
        if (submission.VersionNumber != order.SubmissionVersion)
            throw new AppException(409, "Only the latest pending submission can be reviewed.");
        if (await db.WorkOrderApprovals.AnyAsync(x => x.WorkOrderSubmissionId == submission.Id, ct))
            throw new AppException(409, "This submission already has a final decision.");
        var remarks = decision == WorkOrderReviewDecision.REJECTED
            ? Guard.Required(request.Remarks, "Rejection remarks", 10000)
            : ExecutionRules.Optional(request.Remarks, "Remarks", 10000);
        var supervisor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == currentUser.UserId && x.IsActive &&
            x.UserRoles.Any(role => role.Role.Name == RoleNames.Supervisor), ct)
            ?? throw new AppException(403, "Review requires an active supervisor.");
        var now = clock.GetUtcNow().UtcDateTime;
        var review = new WorkOrderApproval
        {
            WorkOrderId = order.Id,
            WorkOrderSubmissionId = submission.Id,
            SupervisorId = supervisor.Id,
            SupervisorEmployeeId = supervisor.EmployeeId,
            SupervisorName = $"{supervisor.FirstName} {supervisor.LastName}".Trim(),
            Decision = decision,
            Remarks = remarks,
            DecisionAt = now
        };
        db.WorkOrderApprovals.Add(review);
        order.LifecycleStatus = target;
        if (decision == WorkOrderReviewDecision.APPROVED) order.ApprovedAt = now;
        var action = decision == WorkOrderReviewDecision.APPROVED ? "WorkOrder.Approved" : "WorkOrder.Rejected";
        ExecutionHistory.Record(db, order, currentUser, clock, action, submission.Id, remarks, review.SupervisorName);
        audit.Record(action, nameof(WorkOrder), order.Id, newValues: new
        {
            ReviewId = review.Id, SubmissionId = submission.Id, submission.VersionNumber,
            review.SupervisorId, review.Decision, review.DecisionAt, review.Remarks, order.LifecycleStatus
        });
        await MaintainPro.Application.Notifications.WorkflowNotificationHooks.ReviewedAsync(
            db, order, submission, review, currentUser, audit, clock, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ReviewMapping.ToDto(review);
    }
}
