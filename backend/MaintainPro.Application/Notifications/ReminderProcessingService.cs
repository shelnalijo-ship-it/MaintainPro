using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Notifications;

public sealed record ReminderProcessingIssue(Guid? WorkOrderId, string Message);
public sealed record ReminderProcessingSummary(int WorkOrdersEvaluated, int NotificationsCreated,
    int EscalationsCreated, int DuplicatesSkipped, int Errors, IReadOnlyList<ReminderProcessingIssue> Issues,
    int GeneratedWorkOrders = 0, bool HasMoreGeneration = false);

/// <summary>
/// Runs the same durable notification workflow for the hosted service and the management endpoint.
/// Each work order commits independently and conflicts reload all facts before a bounded retry.
/// </summary>
public sealed class ReminderProcessingService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock, NotificationEventService events,
    IGenerationConcurrency concurrency, WorkOrderGenerationService generation)
{
    private readonly WorkOrderTimingService timing = new();

    public async Task<ReminderProcessingSummary> ProcessDueAsync(CancellationToken ct = default)
    {
        // A missing HTTP actor identifies the internal hosted service, as in work-order generation.
        if (currentUser.UserId.HasValue) Guard.RequireRole(currentUser, RoleNames.Manager, RoleNames.Admin);
        if (db.HasActiveTransaction)
            throw new InvalidOperationException("Reminder processing must own its work-order transactions.");
        var settings = await EscalationSettingsService.ReadEffectiveAsync(db, ct);
        var generated = await generation.GenerateUpcomingAsync(settings.DueSoonDays, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var horizon = DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, today.DayNumber + settings.DueSoonDays));

        // Capture only IDs before processing. A fixed snapshot prevents completed rows disappearing
        // from an offset-based query and starving later orders; no arbitrary first-N limit is used.
        var ids = await db.WorkOrders.AsNoTracking().Where(order =>
            (order.SubmittedAt == null && order.LifecycleStatus != WorkOrderLifecycleStatus.APPROVED &&
             order.LifecycleStatus != WorkOrderLifecycleStatus.CANCELLED &&
             order.LifecycleStatus != WorkOrderLifecycleStatus.AWAITING_APPROVAL && order.DueDate <= horizon) ||
            db.WorkOrderEscalations.Any(escalation => escalation.WorkOrderId == order.Id && escalation.ResolvedAt == null) ||
            db.NotificationEvents.Any(notificationEvent => notificationEvent.WorkOrderId == order.Id && notificationEvent.ProcessedAt == null))
            .OrderBy(order => order.DueDate).ThenBy(order => order.Id).Select(order => order.Id).ToListAsync(ct);

        var notifications = generated.NotificationsCreated;
        var escalations = 0;
        var duplicates = 0;
        var errors = generated.Errors;
        var issues = generated.Issues.Select(issue => new ReminderProcessingIssue(null,
            $"Work-order generation: {issue.Message}")).ToList();
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            var result = await ProcessWithRetryAsync(id, now, ct);
            notifications += result.NotificationsCreated;
            escalations += result.EscalationsCreated;
            duplicates += result.DuplicatesSkipped;
            errors += result.Errors;
            issues.AddRange(result.Issues.Select(message => new ReminderProcessingIssue(id, message)));
        }
        return new(ids.Count, notifications, escalations, duplicates, errors, issues,
            generated.WorkOrdersCreated, generated.HasMore);
    }

    private async Task<NotificationDispatchResult> ProcessWithRetryAsync(Guid id, DateTime now, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            concurrency.ResetTracking();
            try { return await ProcessOrderAsync(id, now, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception) when (concurrency.IsRetryable(exception))
            {
                if (attempt == 4)
                    return new(0, 0, 0, 1, ["Reminder processing encountered concurrent changes; retry this work order."]);
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), ct);
            }
            catch (AppException exception) { return new(0, 0, 0, 1, [exception.Message]); }
            catch (Exception)
            {
                // Provider exception messages may contain database/configuration details.
                return new(0, 0, 0, 1, ["Reminder processing failed for this work order; no partial changes were committed."]);
            }
            finally { concurrency.ResetTracking(); }
        }
        throw new InvalidOperationException("The bounded reminder retry loop exited unexpectedly.");
    }

    private async Task<NotificationDispatchResult> ProcessOrderAsync(Guid id, DateTime now, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var order = await db.WorkOrders.Include(x => x.Definition).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null) return new(0, 0, 0, 0, Array.Empty<string>());
        var settings = await EscalationSettingsService.ReadEffectiveAsync(db, ct);
        var state = timing.Evaluate(order, settings, now);
        var results = new List<NotificationDispatchResult>();
        var pendingKeys = (await db.NotificationEvents.AsNoTracking()
            .Where(x => x.WorkOrderId == id && x.ProcessedAt == null)
            .Select(x => x.DeduplicationKey).ToListAsync(ct)).ToHashSet();

        var excluded = order.SubmittedAt.HasValue || order.LifecycleStatus is WorkOrderLifecycleStatus.APPROVED
            or WorkOrderLifecycleStatus.CANCELLED or WorkOrderLifecycleStatus.AWAITING_APPROVAL;
        if (excluded)
        {
            results.Add(await events.ResolveEscalationsAsync(order, ct));
            // Approved work orders are immutable. Resolution only changes the escalation records.
            if (order.LifecycleStatus != WorkOrderLifecycleStatus.APPROVED) order.EscalationLevel = 0;
        }

        // Replay persisted events before ensuring today's events. Skipping their keys below avoids
        // counting the same pending routing failure twice within one processing transaction.
        results.Add(await events.ReplayPendingAsync(order, ct));
        if (!excluded)
        {
            order.EscalationLevel = state.EscalationLevel;
            if (state.IsDueSoon) results.Add(await EnsureAsync(order, NotificationType.WORK_ORDER_DUE_SOON, 0, pendingKeys, ct));
            if (state.IsDueToday) results.Add(await EnsureAsync(order, NotificationType.WORK_ORDER_DUE, 0, pendingKeys, ct));
            // Catch up missed escalation thresholds after downtime, but do not issue past due-soon
            // or due-today reminders. Each semantic type/recipient has a persistent unique key.
            if (state.IsOverdue && state.DaysOverdue >= settings.TechnicianOverdueDays)
                results.Add(await EnsureAsync(order, NotificationType.WORK_ORDER_OVERDUE, 1, pendingKeys, ct));
            if (state.IsOverdue && state.DaysOverdue >= settings.SupervisorEscalationDays)
                results.Add(await EnsureAsync(order, NotificationType.ESCALATION_SUPERVISOR, 2, pendingKeys, ct));
            if (state.IsOverdue && state.DaysOverdue >= settings.ManagerEscalationDays)
                results.Add(await EnsureAsync(order, NotificationType.ESCALATION_MANAGER, 3, pendingKeys, ct));
        }

        var result = Combine(results);
        if (result.NotificationsCreated > 0 || result.EscalationsCreated > 0)
            audit.Record("WorkOrder.RemindersProcessed", nameof(WorkOrder), id, newValues: new
            {
                result.NotificationsCreated, result.EscalationsCreated, state.IsOverdue,
                state.DaysOverdue, state.EscalationLevel
            });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private Task<NotificationDispatchResult> EnsureAsync(WorkOrder order, NotificationType type,
        int escalationLevel, IReadOnlySet<string> pendingKeys, CancellationToken ct)
    {
        var key = $"work-order:{order.Id:N}:{type}:assignment:{order.AssignmentVersion}";
        return pendingKeys.Contains(key)
            ? Task.FromResult(NotificationDispatchResult.Empty)
            : events.EnsureAndDispatchAsync(order, type, key, escalationLevel: escalationLevel, ct: ct);
    }

    private static NotificationDispatchResult Combine(IEnumerable<NotificationDispatchResult> results)
    {
        var entries = results.ToArray();
        return new(entries.Sum(x => x.NotificationsCreated), entries.Sum(x => x.EscalationsCreated),
            entries.Sum(x => x.DuplicatesSkipped), entries.Sum(x => x.Errors),
            entries.SelectMany(x => x.Issues).Distinct().ToArray());
    }
}
