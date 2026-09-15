using MaintainPro.Application.Abstractions;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Notifications;

public static class WorkflowNotificationHooks
{
    public static Task<NotificationDispatchResult> AssignedAsync(IApplicationDbContext db, WorkOrder order,
        ICurrentUser currentUser, IAuditWriter audit, TimeProvider clock, bool reassigned = false,
        CancellationToken ct = default)
    {
        var type = reassigned ? NotificationType.WORK_ORDER_REASSIGNED : NotificationType.WORK_ORDER_ASSIGNED;
        return new NotificationEventService(db, currentUser, audit, clock).EnsureAndDispatchAsync(order, type,
            $"work-order:{order.Id:N}:{type}:assignment:{order.AssignmentVersion}", ct: ct);
    }

    public static async Task<NotificationDispatchResult> SubmittedAsync(IApplicationDbContext db, WorkOrder order,
        WorkOrderSubmission submission, ICurrentUser currentUser, IAuditWriter audit, TimeProvider clock,
        CancellationToken ct = default)
    {
        var service = new NotificationEventService(db, currentUser, audit, clock);
        var resolution = await service.ResolveEscalationsAsync(order, ct);
        var sent = await service.EnsureAndDispatchAsync(order, NotificationType.WORK_ORDER_SUBMITTED,
            $"work-order:{order.Id:N}:submitted:{submission.Id:N}", submission.Id, ct: ct);
        return resolution.Add(sent);
    }

    public static async Task<NotificationDispatchResult> ReviewedAsync(IApplicationDbContext db, WorkOrder order,
        WorkOrderSubmission submission, WorkOrderApproval review, ICurrentUser currentUser, IAuditWriter audit,
        TimeProvider clock, CancellationToken ct = default)
    {
        var type = review.Decision == WorkOrderReviewDecision.APPROVED
            ? NotificationType.WORK_ORDER_APPROVED : NotificationType.WORK_ORDER_REJECTED;
        var service = new NotificationEventService(db, currentUser, audit, clock);
        var resolution = await service.ResolveEscalationsAsync(order, ct);
        var sent = await service.EnsureAndDispatchAsync(order, type,
            $"work-order:{order.Id:N}:{type}:submission:{submission.Id:N}", submission.Id, ct: ct);
        return resolution.Add(sent);
    }
}
