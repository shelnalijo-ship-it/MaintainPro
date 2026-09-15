using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Notifications;

/// <summary>Persists and delivers breakdown notification intents in the caller's business transaction.</summary>
public sealed class BreakdownNotificationService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock)
{
    public async Task<NotificationDispatchResult> EnsureAsync(Breakdown breakdown, NotificationType type,
        Guid? referenceId = null, CancellationToken ct = default)
    {
        RequireTransaction();
        if (type is not (NotificationType.BREAKDOWN_REPORTED or NotificationType.BREAKDOWN_CRITICAL
            or NotificationType.BREAKDOWN_ASSIGNED or NotificationType.CORRECTIVE_SUBMITTED
            or NotificationType.CORRECTIVE_APPROVED or NotificationType.CORRECTIVE_REJECTED))
            throw new AppException(400, "The notification type does not belong to a breakdown.");
        if (type is NotificationType.CORRECTIVE_SUBMITTED or NotificationType.CORRECTIVE_APPROVED
            or NotificationType.CORRECTIVE_REJECTED && referenceId is null)
            throw new AppException(400, "A corrective notification requires its exact submission reference.");

        var key = $"breakdown:{breakdown.Id:N}:{type}";
        if (type == NotificationType.BREAKDOWN_ASSIGNED) key += $":assignment:{breakdown.AssignmentVersion}";
        if (referenceId.HasValue) key += $":reference:{referenceId.Value:N}";
        var intent = db.BreakdownNotificationEvents.Local.SingleOrDefault(x => x.DeduplicationKey == key)
            ?? await db.BreakdownNotificationEvents.SingleOrDefaultAsync(x => x.DeduplicationKey == key, ct);
        if (intent is null)
        {
            var description = Describe(breakdown, type);
            intent = new BreakdownNotificationEvent
            {
                BreakdownId = breakdown.Id, NotificationType = type, EventReferenceId = referenceId,
                AssignmentVersion = breakdown.AssignmentVersion, DeduplicationKey = key,
                Priority = type == NotificationType.BREAKDOWN_CRITICAL ? NotificationPriority.CRITICAL : NotificationPriority.HIGH,
                Title = description.Title, Message = description.Message, CreatedAt = Now
            };
            db.BreakdownNotificationEvents.Add(intent);
            audit.Record("BreakdownNotificationEvent.Created", nameof(Breakdown), breakdown.Id,
                newValues: new { EventId = intent.Id, type, referenceId, ActorUserId = currentUser.UserId });
        }
        return await DispatchAsync(breakdown, intent, ct);
    }

    public async Task<NotificationDispatchResult> ReplayPendingAsync(Breakdown breakdown, CancellationToken ct = default)
    {
        RequireTransaction();
        var pending = await db.BreakdownNotificationEvents.Where(x => x.BreakdownId == breakdown.Id && x.ProcessedAt == null)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var result = NotificationDispatchResult.Empty;
        foreach (var intent in pending)
            if (intent.ProcessedAt is null) result = result.Add(await DispatchAsync(breakdown, intent, ct));
        return result;
    }

    private async Task<NotificationDispatchResult> DispatchAsync(Breakdown breakdown, BreakdownNotificationEvent intent,
        CancellationToken ct)
    {
        if (intent.ProcessedAt.HasValue) return new(0, 0, 1, 0, Array.Empty<string>());
        var obsolete = await ObsoleteReasonAsync(breakdown, intent, ct);
        if (obsolete is not null)
        {
            intent.ProcessedAt = Now;
            intent.LastError = obsolete;
            intent.Version = Guid.NewGuid();
            audit.Record("BreakdownNotificationEvent.Skipped", nameof(Breakdown), breakdown.Id,
                newValues: new { EventId = intent.Id, Reason = obsolete });
            return new(0, 0, 1, 0, Array.Empty<string>());
        }

        var routing = await ResolveRecipientsAsync(breakdown, intent.NotificationType, ct);
        var created = 0;
        var duplicates = 0;
        var writer = new NotificationWriter(db, clock);
        foreach (var userId in routing.UserIds)
        {
            var message = routing.RoutingReason is null ? intent.Message : $"{intent.Message} {routing.RoutingReason}";
            var delivered = await writer.EmitAsync(new NotificationWriteRequest(userId, intent.NotificationType,
                intent.Title, message, intent.Priority, $"{intent.DeduplicationKey}:user:{userId:N}",
                nameof(Breakdown), breakdown.Id), ct);
            if (delivered.Created) created++; else duplicates++;
        }
        intent.Version = Guid.NewGuid();
        if (routing.IsComplete)
        {
            intent.ProcessedAt = Now;
            intent.LastError = null;
            audit.Record("BreakdownNotificationEvent.Dispatched", nameof(Breakdown), breakdown.Id,
                newValues: new { EventId = intent.Id, NotificationsCreated = created, DuplicatesSkipped = duplicates });
            return new(created, 0, duplicates, 0, Array.Empty<string>());
        }

        var reason = routing.RoutingReason ?? "No active recipient is available; delivery will be retried.";
        if (intent.LastError != reason)
            audit.Record("BreakdownNotificationEvent.RoutingIncomplete", nameof(Breakdown), breakdown.Id,
                newValues: new { EventId = intent.Id, Reason = reason });
        intent.LastError = reason;
        return new(created, 0, duplicates, 1, new[] { reason });
    }

    private async Task<NotificationRecipients> ResolveRecipientsAsync(Breakdown breakdown, NotificationType type,
        CancellationToken ct)
    {
        var technicianActive = breakdown.AssignedTechnicianId.HasValue && await db.Users.AnyAsync(x =>
            x.Id == breakdown.AssignedTechnicianId && x.IsActive && x.UserRoles.Any(r => r.Role.Name == RoleNames.Technician), ct);
        var supervisorActive = await db.Users.AnyAsync(x => x.Id == breakdown.SupervisorId && x.IsActive &&
            x.UserRoles.Any(r => r.Role.Name == RoleNames.Supervisor), ct);
        var recipients = new HashSet<Guid>();
        var reasons = new List<string>();
        List<Guid>? managers = null;
        var complete = true;
        async Task ManagersAsync()
        {
            managers ??= await db.Users.Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == RoleNames.Manager))
                .Select(x => x.Id).ToListAsync(ct);
            recipients.UnionWith(managers);
            if (managers.Count == 0) { complete = false; reasons.Add("No active MANAGER recipient is available."); }
        }
        async Task SupervisorAsync()
        {
            if (supervisorActive) recipients.Add(breakdown.SupervisorId);
            else { reasons.Add("No active assigned supervisor; manager attention is required."); await ManagersAsync(); }
        }
        async Task TechnicianAsync()
        {
            if (technicianActive) recipients.Add(breakdown.AssignedTechnicianId!.Value);
            else { reasons.Add("No active assigned technician; supervisor or manager attention is required."); await SupervisorAsync(); }
        }
        switch (type)
        {
            case NotificationType.BREAKDOWN_CRITICAL:
                await SupervisorAsync();
                await ManagersAsync();
                break;
            case NotificationType.BREAKDOWN_REPORTED:
            case NotificationType.CORRECTIVE_SUBMITTED:
                await SupervisorAsync();
                break;
            default:
                await TechnicianAsync();
                break;
        }
        return new(recipients.OrderBy(x => x).ToArray(), reasons.Count == 0 ? null : string.Join(" ", reasons.Distinct()),
            complete && recipients.Count > 0);
    }

    private async Task<string?> ObsoleteReasonAsync(Breakdown breakdown, BreakdownNotificationEvent intent, CancellationToken ct)
    {
        if (intent.NotificationType is NotificationType.BREAKDOWN_REPORTED or NotificationType.BREAKDOWN_CRITICAL
            && breakdown.Status is BreakdownStatus.CLOSED or BreakdownStatus.CANCELLED)
            return "The breakdown was closed or cancelled before report notification delivery.";
        if (intent.NotificationType == NotificationType.BREAKDOWN_ASSIGNED &&
            (intent.AssignmentVersion != breakdown.AssignmentVersion ||
             breakdown.Status is not (BreakdownStatus.REPORTED or BreakdownStatus.ASSIGNED or BreakdownStatus.REJECTED)))
            return "The assignment is superseded or corrective execution has already started.";
        if (intent.NotificationType is NotificationType.CORRECTIVE_SUBMITTED or NotificationType.CORRECTIVE_REJECTED)
        {
            var expected = intent.NotificationType == NotificationType.CORRECTIVE_SUBMITTED
                ? BreakdownStatus.AWAITING_APPROVAL : BreakdownStatus.REJECTED;
            var latest = db.CorrectiveSubmissions.Local.SingleOrDefault(x => x.Id == intent.EventReferenceId);
            var latestReference = latest is not null
                ? latest.BreakdownId == breakdown.Id && latest.VersionNumber == breakdown.SubmissionVersion
                : await db.CorrectiveSubmissions.AnyAsync(x => x.Id == intent.EventReferenceId &&
                    x.BreakdownId == breakdown.Id && x.VersionNumber == breakdown.SubmissionVersion, ct);
            if (breakdown.Status != expected || !latestReference)
                return "The corrective submission no longer requires this action.";
        }
        if (breakdown.Status == BreakdownStatus.CANCELLED && intent.NotificationType != NotificationType.CORRECTIVE_APPROVED)
            return "The breakdown was cancelled before notification delivery.";
        return null;
    }

    private static (string Title, string Message) Describe(Breakdown breakdown, NotificationType type) => type switch
    {
        NotificationType.BREAKDOWN_REPORTED => ("Breakdown reported", $"Breakdown {breakdown.BreakdownNumber} has been reported and requires attention."),
        NotificationType.BREAKDOWN_CRITICAL => ("Critical breakdown reported", $"Critical breakdown {breakdown.BreakdownNumber} requires supervisor and manager attention."),
        NotificationType.BREAKDOWN_ASSIGNED => ("Breakdown assigned", $"The corrective assignment for breakdown {breakdown.BreakdownNumber} is ready for execution."),
        NotificationType.CORRECTIVE_SUBMITTED => ("Corrective work awaiting review", $"Corrective work for breakdown {breakdown.BreakdownNumber} has been submitted for supervisor review."),
        NotificationType.CORRECTIVE_APPROVED => ("Corrective work approved", $"Corrective work for breakdown {breakdown.BreakdownNumber} was approved and the breakdown was closed."),
        NotificationType.CORRECTIVE_REJECTED => ("Corrective changes required", $"Corrective work for breakdown {breakdown.BreakdownNumber} was rejected and requires correction."),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private void RequireTransaction()
    {
        if (!db.HasActiveTransaction) throw new InvalidOperationException("Breakdown notification dispatch requires the business transaction.");
    }
}
