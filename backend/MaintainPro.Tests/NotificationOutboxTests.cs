using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Notifications;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class NotificationOutboxTests
{
    [Fact]
    public async Task Pending_manager_event_obeys_raised_threshold_and_delivers_once_when_it_becomes_due()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture, -5);
        var first = await fixture.Reminders.ProcessDueAsync();
        Assert.True(first.Errors > 0);
        var pending = await fixture.Db.NotificationEvents.SingleAsync(x => x.NotificationType == NotificationType.ESCALATION_MANAGER);
        Assert.Null(pending.ProcessedAt);
        var eventId = pending.Id;
        await fixture.EscalationSettings.UpdateAsync(new(1, 1, 3, 10));
        var manager = await fixture.SeedUserAsync("MANAGER");

        var deferred = await fixture.Reminders.ProcessDueAsync();

        Assert.Equal(0, deferred.Errors);
        Assert.Equal(0, deferred.NotificationsCreated);
        pending = await fixture.Db.NotificationEvents.SingleAsync(x => x.Id == eventId);
        Assert.Null(pending.ProcessedAt);
        Assert.Contains("threshold", pending.LastError!, StringComparison.OrdinalIgnoreCase);
        fixture.Clock.Advance(TimeSpan.FromDays(5));
        var delivered = await fixture.Reminders.ProcessDueAsync();
        Assert.Equal(1, delivered.NotificationsCreated);
        Assert.Equal(1, delivered.EscalationsCreated);
        Assert.NotNull((await fixture.Db.NotificationEvents.SingleAsync(x => x.Id == eventId)).ProcessedAt);
        var notification = Assert.Single(await fixture.Db.Notifications.Where(x => x.NotificationType == NotificationType.ESCALATION_MANAGER).ToListAsync());
        Assert.Equal(manager.Id, notification.UserId);
        Assert.Equal(0, (await fixture.Reminders.ProcessDueAsync()).NotificationsCreated);
    }

    [Fact]
    public async Task Expired_due_soon_outbox_notice_is_skipped_and_due_today_is_delivered_instead()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture, 1);
        var users = await fixture.Db.Users.Where(x => x.Id == data.Technician.Id || x.Id == data.Supervisor.Id).ToListAsync();
        foreach (var user in users) user.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        Assert.True((await fixture.Reminders.ProcessDueAsync()).Errors > 0);
        var pendingId = await fixture.Db.NotificationEvents.Where(x => x.NotificationType == NotificationType.WORK_ORDER_DUE_SOON).Select(x => x.Id).SingleAsync();
        users = await fixture.Db.Users.Where(x => x.Id == data.Technician.Id || x.Id == data.Supervisor.Id).ToListAsync();
        foreach (var user in users) user.IsActive = true;
        await fixture.Db.SaveChangesAsync();
        fixture.Clock.Advance(TimeSpan.FromDays(1));

        Assert.Equal(0, (await fixture.Reminders.ProcessDueAsync()).Errors);

        var expired = await fixture.Db.NotificationEvents.SingleAsync(x => x.Id == pendingId);
        Assert.NotNull(expired.ProcessedAt);
        Assert.Contains("window", expired.LastError!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await fixture.Db.Notifications.Where(x => x.NotificationType == NotificationType.WORK_ORDER_DUE_SOON).ToListAsync());
        Assert.Single(await fixture.Db.Notifications.Where(x => x.NotificationType == NotificationType.WORK_ORDER_DUE).ToListAsync());
    }

    [Fact]
    public async Task Submission_notice_pending_routing_is_not_delivered_after_supervisor_already_reviews_it()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture);
        var supervisor = await fixture.Db.Users.SingleAsync(x => x.Id == data.Supervisor.Id);
        supervisor.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        var submission = await data.SubmitAsync(fixture);
        var pendingId = await fixture.Db.NotificationEvents.Where(x => x.NotificationType == NotificationType.WORK_ORDER_SUBMITTED).Select(x => x.Id).SingleAsync();
        supervisor = await fixture.Db.Users.SingleAsync(x => x.Id == data.Supervisor.Id);
        supervisor.IsActive = true;
        await fixture.Db.SaveChangesAsync();
        fixture.ActAs(data.Supervisor);
        await fixture.Reviews.ApproveAsync(data.WorkOrder.Id, new(submission.Submission.Id));
        fixture.ActAs(data.Administrator);

        Assert.Equal(0, (await fixture.Reminders.ProcessDueAsync()).Errors);

        Assert.Empty(await fixture.Db.Notifications.Where(x => x.NotificationType == NotificationType.WORK_ORDER_SUBMITTED).ToListAsync());
        var obsolete = await fixture.Db.NotificationEvents.SingleAsync(x => x.Id == pendingId);
        Assert.NotNull(obsolete.ProcessedAt);
        Assert.Contains("decision", obsolete.LastError!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WorkOrderLifecycleStatus.APPROVED, (await fixture.Db.WorkOrders.SingleAsync()).LifecycleStatus);
    }

    [Fact]
    public async Task Reminder_audit_failure_rolls_back_events_delivery_escalations_and_work_order_changes_together()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture, -5);
        await fixture.SeedUserAsync("MANAGER");
        var notifications = await fixture.Db.Notifications.CountAsync();
        var events = await fixture.Db.NotificationEvents.CountAsync();
        var history = await fixture.Db.WorkOrderHistoryEvents.CountAsync();
        var audit = new FailingAudit();
        var processor = new ReminderProcessingService(fixture.Db, fixture.Actor, audit, fixture.Clock,
            new NotificationEventService(fixture.Db, fixture.Actor, audit, fixture.Clock),
            new SqliteGenerationConcurrency(fixture.Db), fixture.Generation);

        var result = await processor.ProcessDueAsync();

        Assert.Equal(1, result.Errors);
        Assert.Equal(0, result.NotificationsCreated);
        Assert.Equal(notifications, await fixture.Db.Notifications.CountAsync());
        Assert.Equal(notifications * 3, await fixture.Db.NotificationDeliveryAttempts.CountAsync());
        Assert.Equal(events, await fixture.Db.NotificationEvents.CountAsync());
        Assert.Equal(history, await fixture.Db.WorkOrderHistoryEvents.CountAsync());
        Assert.Empty(await fixture.Db.WorkOrderEscalations.ToListAsync());
        var persisted = await fixture.Db.WorkOrders.SingleAsync();
        Assert.Equal(0, persisted.EscalationLevel);
        Assert.Equal(data.WorkOrder.LifecycleStatus, persisted.LifecycleStatus);
    }

    private sealed class FailingAudit : IAuditWriter
    {
        public void Record(string action, string entityType, Guid? entityId, object? oldValues = null,
            object? newValues = null) => throw new InvalidOperationException("Simulated reminder audit failure.");
    }
}
