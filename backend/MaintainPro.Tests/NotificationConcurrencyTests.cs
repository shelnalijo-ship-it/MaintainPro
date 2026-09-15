using MaintainPro.Application.Notifications;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class NotificationConcurrencyTests
{
    [Fact]
    public async Task Concurrent_processors_and_a_restarted_instance_create_each_notification_and_escalation_once()
    {
        using var database = new TemporaryNotificationDatabase();
        DateTimeOffset processingTime;
        Guid orderId;
        await using (var first = await ModuleFixture.CreateAsync(sqliteDatabasePath: database.Path))
        await using (var second = await ModuleFixture.CreateAsync(seedRoles: false, sqliteDatabasePath: database.Path))
        {
            var data = await NotificationTestData.CreateAsync(first, dueOffsetDays: -5);
            await first.SeedUserAsync("MANAGER");
            await first.SeedUserAsync("MANAGER");
            processingTime = first.Clock.GetUtcNow();
            orderId = data.WorkOrder.Id;
            second.ActAs(data.Administrator);
            second.Clock.SetUtc(processingTime);
            var initialNotifications = await first.Db.Notifications.CountAsync();
            using var overlap = new Barrier(2);
            ReminderProcessingService Processor(ModuleFixture fixture) => new(fixture.Db, fixture.Actor,
                fixture.Audit, fixture.Clock, fixture.NotificationEvents,
                new OverlappingReminders(fixture, overlap), fixture.Generation);
            var firstProcessor = Processor(first);
            var secondProcessor = Processor(second);

            var results = await Task.WhenAll(Task.Run(() => firstProcessor.ProcessDueAsync()),
                Task.Run(() => secondProcessor.ProcessDueAsync()));

            Assert.All(results, result => Assert.Equal(0, result.Errors));
            // Day one: technician + supervisor; day three: supervisor; day five: two managers.
            Assert.Equal(5, results.Sum(x => x.NotificationsCreated));
            Assert.Equal(5, results.Sum(x => x.EscalationsCreated));
            first.Db.ChangeTracker.Clear();
            var notifications = await first.Db.Notifications.AsNoTracking().ToListAsync();
            Assert.Equal(initialNotifications + 5, notifications.Count);
            Assert.Equal(notifications.Count, notifications.Select(x => x.DeduplicationKey).Distinct().Count());
            var escalations = await first.Db.WorkOrderEscalations.AsNoTracking().ToListAsync();
            Assert.Equal(5, escalations.Count);
            Assert.Equal(5, escalations.Select(x => x.DeduplicationKey).Distinct().Count());
            Assert.Equal(3, await first.Db.WorkOrderHistoryEvents.CountAsync(x => x.WorkOrderId == orderId &&
                (x.Action == "WorkOrder.OverdueDetected" || x.Action == "WorkOrder.SupervisorEscalated" ||
                 x.Action == "WorkOrder.ManagerEscalated")));
            Assert.Equal(notifications.Count * 3, await first.Db.NotificationDeliveryAttempts.CountAsync());
            Assert.Equal(WorkOrderLifecycleStatus.ASSIGNED,
                (await first.Db.WorkOrders.SingleAsync(x => x.Id == orderId)).LifecycleStatus);
        }

        await using var restarted = await ModuleFixture.CreateAsync(seedRoles: false, sqliteDatabasePath: database.Path);
        restarted.Clock.SetUtc(processingTime);
        var administrator = await restarted.Db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleAsync(x => x.UserRoles.Any(role => role.Role.Name == "ADMIN"));
        restarted.ActAs(administrator);
        var repeated = await restarted.Reminders.ProcessDueAsync();
        Assert.Equal(0, repeated.Errors);
        Assert.Equal(0, repeated.NotificationsCreated);
        Assert.Equal(0, repeated.EscalationsCreated);
        Assert.Equal(5, await restarted.Db.WorkOrderEscalations.CountAsync());
    }

    [Fact]
    public async Task Concurrent_retry_of_partial_routing_delivers_only_the_previously_missing_recipient()
    {
        using var database = new TemporaryNotificationDatabase();
        await using var first = await ModuleFixture.CreateAsync(sqliteDatabasePath: database.Path);
        await using var second = await ModuleFixture.CreateAsync(seedRoles: false, sqliteDatabasePath: database.Path);
        var data = await NotificationTestData.CreateAsync(first, dueOffsetDays: -1);
        (await first.Db.Users.SingleAsync(x => x.Id == data.Supervisor.Id)).IsActive = false;
        await first.Db.SaveChangesAsync();
        var partial = await first.Reminders.ProcessDueAsync();
        Assert.True(partial.Errors > 0);
        Assert.Single(await first.Db.Notifications.Where(x => x.NotificationType == NotificationType.WORK_ORDER_OVERDUE).ToListAsync());
        Assert.Single(await first.Db.NotificationEvents.Where(x => x.NotificationType == NotificationType.WORK_ORDER_OVERDUE &&
            x.ProcessedAt == null).ToListAsync());
        var manager = await first.SeedUserAsync("MANAGER");
        second.ActAs(data.Administrator);
        second.Clock.SetUtc(first.Clock.GetUtcNow());
        using var overlap = new Barrier(2);
        ReminderProcessingService Processor(ModuleFixture fixture) => new(fixture.Db, fixture.Actor, fixture.Audit,
            fixture.Clock, fixture.NotificationEvents, new OverlappingReminders(fixture, overlap), fixture.Generation);
        var firstProcessor = Processor(first);
        var secondProcessor = Processor(second);

        var retried = await Task.WhenAll(Task.Run(() => firstProcessor.ProcessDueAsync()),
            Task.Run(() => secondProcessor.ProcessDueAsync()));

        Assert.All(retried, x => Assert.Equal(0, x.Errors));
        Assert.Equal(1, retried.Sum(x => x.NotificationsCreated));
        Assert.Equal(1, retried.Sum(x => x.EscalationsCreated));
        first.Db.ChangeTracker.Clear();
        var delivered = await first.Db.Notifications.Where(x => x.NotificationType == NotificationType.WORK_ORDER_OVERDUE).ToListAsync();
        Assert.Equal(2, delivered.Count);
        Assert.Contains(delivered, x => x.UserId == data.Technician.Id);
        Assert.Contains(delivered, x => x.UserId == manager.Id);
        Assert.DoesNotContain(delivered, x => x.UserId == data.Supervisor.Id);
        Assert.Empty(await first.Db.NotificationEvents.Where(x => x.ProcessedAt == null).ToListAsync());
        Assert.Single(await first.Db.WorkOrderHistoryEvents.Where(x => x.Action == "WorkOrder.OverdueDetected").ToListAsync());
    }

    private sealed class OverlappingReminders(ModuleFixture fixture, Barrier overlap) : IGenerationConcurrency
    {
        private readonly SqliteGenerationConcurrency inner = new(fixture.Db);
        private int entered;
        public void ResetTracking()
        {
            inner.ResetTracking();
            if (Interlocked.Exchange(ref entered, 1) == 0)
                Assert.True(overlap.SignalAndWait(TimeSpan.FromSeconds(20)),
                    "Both processors must capture the same candidate before either starts its work-order transaction.");
        }
        public bool IsRetryable(Exception exception) => inner.IsRetryable(exception);
    }

    private sealed class TemporaryNotificationDatabase : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            $"maintainpro-notifications-test-{Guid.NewGuid():N}.sqlite");
        public void Dispose()
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" }) File.Delete(Path + suffix);
        }
    }
}
