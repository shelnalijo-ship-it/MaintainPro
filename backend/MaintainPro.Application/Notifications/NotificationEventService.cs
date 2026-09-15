using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Execution;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Notifications;

/// <summary>Durable in-app event dispatch. All methods participate in the caller's business transaction.</summary>
public sealed class NotificationEventService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock)
{
    public async Task<NotificationDispatchResult> EnsureAndDispatchAsync(WorkOrder order, NotificationType type,
        string dedupKey, Guid? eventReferenceId = null, int escalationLevel = 0, CancellationToken ct = default)
    {
        RequireTransaction();
        if (!Enum.IsDefined(type) || escalationLevel is < 0 or > 3)
            throw new AppException(400, "Notification event type or escalation level is invalid.");
        var key = Guard.Required(dedupKey, "DeduplicationKey", 500);
        var notificationEvent = db.NotificationEvents.Local.SingleOrDefault(x => x.DeduplicationKey == key)
            ?? await db.NotificationEvents.SingleOrDefaultAsync(x => x.DeduplicationKey == key, ct);
        if (notificationEvent is null)
        {
            var text = Describe(order, type);
            notificationEvent = new NotificationEvent
            {
                WorkOrderId = order.Id,
                NotificationType = type,
                EventReferenceId = eventReferenceId,
                AssignmentVersion = order.AssignmentVersion,
                Priority = Priority(order, type),
                Title = text.Title,
                Message = text.Message,
                DeduplicationKey = key,
                CreatedAt = Now,
                EscalationLevel = escalationLevel
            };
            db.NotificationEvents.Add(notificationEvent);
            await RecordTimingHistoryAsync(order, notificationEvent, ct);
        }
        else if (notificationEvent.WorkOrderId != order.Id || notificationEvent.NotificationType != type ||
            notificationEvent.EventReferenceId != eventReferenceId || notificationEvent.EscalationLevel != escalationLevel)
            throw new AppException(409, "The event key belongs to a different work-order event.");

        return await DispatchAsync(order, notificationEvent, ct);
    }

    public async Task<NotificationDispatchResult> ReplayPendingAsync(WorkOrder order, CancellationToken ct = default)
    {
        RequireTransaction();
        var pending = await db.NotificationEvents.Where(x => x.WorkOrderId == order.Id && x.ProcessedAt == null)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var result = NotificationDispatchResult.Empty;
        foreach (var notificationEvent in pending)
            if (notificationEvent.ProcessedAt is null)
                result = result.Add(await DispatchAsync(order, notificationEvent, ct));
        return result;
    }

    public async Task<NotificationDispatchResult> ResolveEscalationsAsync(WorkOrder order, CancellationToken ct = default)
    {
        RequireTransaction();
        if (order.SubmittedAt is null && order.LifecycleStatus is not (WorkOrderLifecycleStatus.APPROVED or
            WorkOrderLifecycleStatus.CANCELLED or WorkOrderLifecycleStatus.AWAITING_APPROVAL))
            return NotificationDispatchResult.Empty;
        var unresolved = await db.WorkOrderEscalations.Where(x => x.WorkOrderId == order.Id && x.ResolvedAt == null).ToListAsync(ct);
        var now = Now;
        foreach (var escalation in unresolved) escalation.ResolvedAt = now;
        if (order.LifecycleStatus != WorkOrderLifecycleStatus.APPROVED) order.EscalationLevel = 0;
        if (unresolved.Count > 0)
        {
            if (order.LifecycleStatus != WorkOrderLifecycleStatus.APPROVED)
                ExecutionHistory.Record(db, order, currentUser, clock, "WorkOrder.EscalationsResolved",
                    details: "Execution reminders resolved after submission or closure.");
            audit.Record("WorkOrder.EscalationsResolved", nameof(WorkOrder), order.Id,
                newValues: new { ResolvedCount = unresolved.Count, ResolvedAt = now });
        }
        return NotificationDispatchResult.Empty;
    }

    private async Task<NotificationDispatchResult> DispatchAsync(WorkOrder order, NotificationEvent notificationEvent, CancellationToken ct)
    {
        if (notificationEvent.ProcessedAt.HasValue) return new(0, 0, 1, 0, Array.Empty<string>());
        var obsolete = await ObsoleteReasonAsync(order, notificationEvent, ct);
        if (obsolete is not null)
        {
            notificationEvent.ProcessedAt = Now;
            notificationEvent.LastError = obsolete;
            notificationEvent.Version = Guid.NewGuid();
            audit.Record("NotificationEvent.Skipped", nameof(WorkOrder), order.Id,
                newValues: new { EventId = notificationEvent.Id, notificationEvent.NotificationType, Reason = obsolete });
            return new(0, 0, 1, 0, Array.Empty<string>());
        }

        if (!await EligibleUnderCurrentPolicyAsync(order, notificationEvent.NotificationType, ct))
        {
            const string deferred = "Deferred until the current escalation policy threshold is reached.";
            if (notificationEvent.LastError != deferred)
            {
                notificationEvent.LastError = deferred;
                notificationEvent.Version = Guid.NewGuid();
                audit.Record("NotificationEvent.Deferred", nameof(WorkOrder), order.Id,
                    newValues: new { EventId = notificationEvent.Id, Reason = deferred });
            }
            return NotificationDispatchResult.Empty;
        }

        var routing = await NotificationRecipientResolver.ResolveAsync(db, order, notificationEvent.NotificationType, ct);
        var notifications = 0;
        var escalations = 0;
        var duplicates = 0;
        var writer = new NotificationWriter(db, clock);
        foreach (var recipientId in routing.UserIds)
        {
            // Reassignment events are distinct real assignments; reminder events remain semantic per job/type/user.
            var reference = notificationEvent.NotificationType is NotificationType.WORK_ORDER_ASSIGNED or NotificationType.WORK_ORDER_REASSIGNED
                ? notificationEvent.Id : notificationEvent.EventReferenceId;
            var key = NotificationDeduplication.ForWorkOrder(order.Id, notificationEvent.NotificationType, recipientId, reference);
            var message = routing.RoutingReason is null ? notificationEvent.Message : $"{notificationEvent.Message} {routing.RoutingReason}";
            var written = await writer.EmitAsync(new(recipientId, notificationEvent.NotificationType,
                notificationEvent.Title, message, notificationEvent.Priority, key, nameof(WorkOrder), order.Id), ct);
            if (written.Created) notifications++;
            else duplicates++;
            if (notificationEvent.EscalationLevel > 0)
            {
                var escalationKey = $"escalation:{order.Id:N}:{notificationEvent.NotificationType}:user:{recipientId:N}";
                var exists = db.WorkOrderEscalations.Local.Any(x => x.DeduplicationKey == escalationKey) ||
                    await db.WorkOrderEscalations.AnyAsync(x => x.DeduplicationKey == escalationKey, ct);
                if (!exists)
                {
                    db.WorkOrderEscalations.Add(new WorkOrderEscalation
                    {
                        WorkOrderId = order.Id, Level = notificationEvent.EscalationLevel,
                        TriggerType = notificationEvent.NotificationType, TriggeredAt = Now,
                        RecipientUserId = recipientId, NotificationId = written.NotificationId,
                        DeduplicationKey = escalationKey, RoutingReason = routing.RoutingReason
                    });
                    escalations++;
                }
            }
        }

        var error = routing.IsComplete ? null : routing.RoutingReason ?? "No active notification recipient is available.";
        if (routing.IsComplete) notificationEvent.ProcessedAt = Now;
        if (notificationEvent.LastError != error && error is not null)
            audit.Record("NotificationEvent.RoutingPending", nameof(WorkOrder), order.Id,
                newValues: new { EventId = notificationEvent.Id, notificationEvent.NotificationType, Reason = error });
        if (notifications > 0 || escalations > 0)
            audit.Record("NotificationEvent.Dispatched", nameof(WorkOrder), order.Id,
                newValues: new { EventId = notificationEvent.Id, notificationEvent.NotificationType,
                    NotificationsCreated = notifications, EscalationsCreated = escalations, RoutingReason = routing.RoutingReason });
        notificationEvent.LastError = error;
        notificationEvent.Version = Guid.NewGuid();
        return new(notifications, escalations, duplicates, error is null ? 0 : 1,
            error is null ? Array.Empty<string>() : new[] { error });
    }

    private async Task<string?> ObsoleteReasonAsync(WorkOrder order, NotificationEvent notificationEvent, CancellationToken ct)
    {
        var type = notificationEvent.NotificationType;
        var executionReminder = type is NotificationType.WORK_ORDER_DUE_SOON or NotificationType.WORK_ORDER_DUE or
            NotificationType.WORK_ORDER_OVERDUE or NotificationType.ESCALATION_SUPERVISOR or NotificationType.ESCALATION_MANAGER;
        if ((executionReminder || type is NotificationType.WORK_ORDER_ASSIGNED or NotificationType.WORK_ORDER_REASSIGNED)
            && notificationEvent.AssignmentVersion != order.AssignmentVersion)
            return "Superseded by a later work-order assignment.";
        if (type is NotificationType.WORK_ORDER_ASSIGNED or NotificationType.WORK_ORDER_REASSIGNED &&
            (order.StartedAt.HasValue || order.LifecycleStatus is not (WorkOrderLifecycleStatus.PLANNED or WorkOrderLifecycleStatus.ASSIGNED)))
            return "The assignment notice is obsolete because execution has already started or the work order closed.";
        if (executionReminder && (order.SubmittedAt.HasValue || order.LifecycleStatus is WorkOrderLifecycleStatus.APPROVED or
            WorkOrderLifecycleStatus.CANCELLED or WorkOrderLifecycleStatus.AWAITING_APPROVAL))
            return "Execution reminders ended after submission or closure.";
        var today = DateOnly.FromDateTime(Now);
        if (type == NotificationType.WORK_ORDER_DUE_SOON && order.DueDate <= today)
            return "The due-soon reminder window has ended.";
        if (type == NotificationType.WORK_ORDER_DUE && order.DueDate != today)
            return "The due-date reminder window has ended.";
        if (type == NotificationType.WORK_ORDER_SUBMITTED &&
            (order.LifecycleStatus != WorkOrderLifecycleStatus.AWAITING_APPROVAL ||
             !await IsLatestSubmissionAsync(order, notificationEvent.EventReferenceId, ct)))
            return "The referenced submission is no longer awaiting a decision.";
        if (type == NotificationType.WORK_ORDER_REJECTED &&
            (order.LifecycleStatus != WorkOrderLifecycleStatus.REJECTED ||
             !await IsLatestSubmissionAsync(order, notificationEvent.EventReferenceId, ct)))
            return "The rejected submission has already been resumed or superseded.";
        return null;
    }

    private async Task<bool> IsLatestSubmissionAsync(WorkOrder order, Guid? submissionId, CancellationToken ct) =>
        db.WorkOrderSubmissions.Local.Any(x => x.Id == submissionId && x.WorkOrderId == order.Id &&
            x.VersionNumber == order.SubmissionVersion) ||
        await db.WorkOrderSubmissions.AnyAsync(x => x.Id == submissionId && x.WorkOrderId == order.Id &&
            x.VersionNumber == order.SubmissionVersion, ct);

    private async Task<bool> EligibleUnderCurrentPolicyAsync(WorkOrder order, NotificationType type, CancellationToken ct)
    {
        if (type is not (NotificationType.WORK_ORDER_DUE_SOON or NotificationType.WORK_ORDER_DUE or
            NotificationType.WORK_ORDER_OVERDUE or NotificationType.ESCALATION_SUPERVISOR or NotificationType.ESCALATION_MANAGER))
            return true;
        var settings = await EscalationSettingsService.ReadEffectiveAsync(db, ct);
        var state = new WorkOrderTimingService().Evaluate(order, settings, Now);
        return type switch
        {
            NotificationType.WORK_ORDER_DUE_SOON => state.IsDueSoon,
            NotificationType.WORK_ORDER_DUE => state.IsDueToday,
            NotificationType.WORK_ORDER_OVERDUE => state.IsOverdue && state.DaysOverdue >= settings.TechnicianOverdueDays,
            NotificationType.ESCALATION_SUPERVISOR => state.IsOverdue && state.DaysOverdue >= settings.SupervisorEscalationDays,
            NotificationType.ESCALATION_MANAGER => state.IsOverdue && state.DaysOverdue >= settings.ManagerEscalationDays,
            _ => true
        };
    }

    private async Task RecordTimingHistoryAsync(WorkOrder order, NotificationEvent notificationEvent, CancellationToken ct)
    {
        if (order.SubmittedAt.HasValue || order.LifecycleStatus is WorkOrderLifecycleStatus.APPROVED or
            WorkOrderLifecycleStatus.CANCELLED or WorkOrderLifecycleStatus.AWAITING_APPROVAL) return;
        var action = notificationEvent.NotificationType switch
        {
            NotificationType.WORK_ORDER_DUE_SOON => "WorkOrder.DueSoonReminder",
            NotificationType.WORK_ORDER_DUE => "WorkOrder.DueReminder",
            NotificationType.WORK_ORDER_OVERDUE => "WorkOrder.OverdueDetected",
            NotificationType.ESCALATION_SUPERVISOR => "WorkOrder.SupervisorEscalated",
            NotificationType.ESCALATION_MANAGER => "WorkOrder.ManagerEscalated",
            _ => null
        };
        if (action is not null &&
            !db.NotificationEvents.Local.Any(x => x.Id != notificationEvent.Id && x.WorkOrderId == order.Id &&
                x.NotificationType == notificationEvent.NotificationType) &&
            !await db.NotificationEvents.AnyAsync(x => x.Id != notificationEvent.Id && x.WorkOrderId == order.Id &&
                x.NotificationType == notificationEvent.NotificationType, ct))
            ExecutionHistory.Record(db, order, currentUser, clock, action, details: notificationEvent.Message);
    }

    private static NotificationPriority Priority(WorkOrder order, NotificationType type) =>
        order.Priority == MaintenancePriority.CRITICAL ? NotificationPriority.CRITICAL :
        type is NotificationType.WORK_ORDER_OVERDUE or NotificationType.ESCALATION_SUPERVISOR or NotificationType.ESCALATION_MANAGER
            or NotificationType.WORK_ORDER_REJECTED || order.Priority == MaintenancePriority.HIGH
            ? NotificationPriority.HIGH : NotificationPriority.NORMAL;

    private static (string Title, string Message) Describe(WorkOrder order, NotificationType type) => type switch
    {
        NotificationType.WORK_ORDER_ASSIGNED => ("Maintenance assignment", $"Work order {order.WorkOrderNumber} is ready for technician assignment or execution."),
        NotificationType.WORK_ORDER_REASSIGNED => ("Maintenance reassigned", $"The technician assignment for work order {order.WorkOrderNumber} changed."),
        NotificationType.WORK_ORDER_DUE_SOON => ("Maintenance due soon", $"Work order {order.WorkOrderNumber} is due on {order.DueDate:yyyy-MM-dd}."),
        NotificationType.WORK_ORDER_DUE => ("Maintenance due today", $"Work order {order.WorkOrderNumber} is due today."),
        NotificationType.WORK_ORDER_OVERDUE => ("Maintenance overdue", $"Work order {order.WorkOrderNumber} is overdue and requires attention."),
        NotificationType.WORK_ORDER_SUBMITTED => ("Maintenance awaiting review", $"Work order {order.WorkOrderNumber} was submitted for supervisor review."),
        NotificationType.WORK_ORDER_APPROVED => ("Maintenance approved", $"A submitted version of work order {order.WorkOrderNumber} was approved."),
        NotificationType.WORK_ORDER_REJECTED => ("Maintenance correction required", $"A submitted version of work order {order.WorkOrderNumber} was rejected. Review its decision remarks before correcting the work."),
        NotificationType.ESCALATION_SUPERVISOR => ("Supervisor attention required", $"Overdue work order {order.WorkOrderNumber} reached the supervisor escalation threshold."),
        NotificationType.ESCALATION_MANAGER => ("Manager attention required", $"Overdue work order {order.WorkOrderNumber} reached the manager escalation threshold."),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private void RequireTransaction()
    {
        if (!db.HasActiveTransaction) throw new InvalidOperationException("Notification events require the business transaction.");
    }
}
