using MaintainPro.Application.Calibrations;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class CalibrationReminderTests
{
    [Fact]
    public async Task Daily_processing_emits_each_crossed_threshold_once_and_is_restart_safe()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        await CalibrationTestData.AddCertificateAsync(fixture, data, expiresInDays: 60);

        Assert.Equal(2, (await fixture.CalibrationReminders.ProcessAsync()).NotificationsCreated);
        fixture.Clock.Advance(TimeSpan.FromDays(30));
        Assert.Equal(2, (await fixture.CalibrationReminders.ProcessAsync()).NotificationsCreated);
        fixture.Clock.Advance(TimeSpan.FromDays(23));
        Assert.Equal(2, (await fixture.CalibrationReminders.ProcessAsync()).NotificationsCreated);
        fixture.Clock.Advance(TimeSpan.FromDays(7));
        var expiry = await fixture.CalibrationReminders.ProcessAsync();
        Assert.Equal(2, expiry.NotificationsCreated);
        Assert.Equal(1, expiry.ExpiredDetected);

        var repeat = await fixture.CalibrationReminders.ProcessAsync();
        Assert.Equal(0, repeat.NotificationsCreated);
        Assert.Equal(4, await fixture.Db.CalibrationNotificationEvents.CountAsync());
        var notifications = await fixture.Db.Notifications.ToListAsync();
        Assert.Equal(8, notifications.Count);
        foreach (var type in new[] { NotificationType.CALIBRATION_60_DAY, NotificationType.CALIBRATION_30_DAY,
                     NotificationType.CALIBRATION_7_DAY, NotificationType.CALIBRATION_EXPIRED })
        {
            Assert.Equal(2, notifications.Count(x => x.NotificationType == type));
            Assert.All(notifications.Where(x => x.NotificationType == type),
                x => Assert.Equal(data.Machine.Id, x.EntityId));
        }
    }

    [Fact]
    public async Task Missing_certificate_is_expired_and_routes_to_supervisor_and_manager()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);

        var result = await fixture.CalibrationReminders.ProcessAsync();

        Assert.Equal(1, result.ExpiredDetected);
        Assert.Equal(2, result.NotificationsCreated);
        var recipients = await fixture.Db.Notifications.Select(x => x.UserId).ToListAsync();
        Assert.Equal(new[] { data.Manager.Id, data.Supervisor.Id }.Order(), recipients.Order());
    }

    [Fact]
    public async Task Inactive_supervisor_falls_back_to_managers()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        data.Supervisor.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        await CalibrationTestData.AddCertificateAsync(fixture, data, expiresInDays: 7);

        var result = await fixture.CalibrationReminders.ProcessAsync();

        Assert.Equal(3, result.NotificationsCreated);
        Assert.All(await fixture.Db.Notifications.ToListAsync(), x => Assert.Equal(data.Manager.Id, x.UserId));
        Assert.DoesNotContain(await fixture.Db.Notifications.ToListAsync(), x => x.UserId == data.Supervisor.Id);
    }

    [Fact]
    public async Task Multi_role_supervisor_manager_is_deduplicated()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        fixture.Clock.SetUtc(now);
        var recipient = await fixture.SeedUserAsync("SUPERVISOR", "MANAGER");
        var technician = await fixture.SeedUserAsync("TECHNICIAN");
        var machine = await fixture.SeedMachineAsync(technician.Id, recipient.Id,
            x => x.CalibrationRequired = true);
        fixture.ActAs(recipient);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        await fixture.Calibrations.CreateAsync(new(machine.Id, "ONE", "LAB", today.AddDays(-1),
            today.AddDays(60), CalibrationResult.PASS));

        var result = await fixture.CalibrationReminders.ProcessAsync();

        Assert.Equal(1, result.NotificationsCreated);
        Assert.Single(await fixture.Db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Manual_processing_requires_manager_or_admin()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        fixture.ActAs(data.Supervisor);
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.CalibrationReminders.ProcessAsync());
        fixture.ActAs(data.Administrator);
        Assert.Equal(1, (await fixture.CalibrationReminders.ProcessAsync()).MachinesEvaluated);
    }

    [Fact]
    public async Task Summary_uses_non_overlapping_primary_states_and_overlapping_windows()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        await AddAsync(data.Machine, "VALID", 90);
        await AddMachineAsync("D60", 50);
        await AddMachineAsync("D30", 20);
        await AddMachineAsync("D7", 5);
        var expiredMachine = await AddMachineAsync("EXPIRED", 0);
        await fixture.CalibrationRenewals.StartAsync(expiredMachine.Id, new());

        var summary = await fixture.Calibrations.SummaryAsync();

        Assert.Equal(5, summary.TotalCalibrationRequiredMachines);
        Assert.Equal(1, summary.Valid);
        Assert.Equal(3, summary.ExpiringWithin60Days);
        Assert.Equal(2, summary.ExpiringWithin30Days);
        Assert.Equal(1, summary.ExpiringWithin7Days);
        Assert.Equal(1, summary.Expired);
        Assert.Equal(1, summary.RenewalInProgress);

        async Task<MaintainPro.Domain.Entities.Machine> AddMachineAsync(string number, int days)
        {
            var machine = await fixture.SeedMachineAsync(data.Technician.Id, data.Supervisor.Id,
                x => x.CalibrationRequired = true);
            await AddAsync(machine, number, days);
            return machine;
        }

        async Task AddAsync(MaintainPro.Domain.Entities.Machine machine, string number, int days) =>
            await fixture.Calibrations.CreateAsync(new(machine.Id, number, "LAB", data.Today.AddDays(-10),
                data.Today.AddDays(days), CalibrationResult.PASS));
    }

    [Fact]
    public async Task Concurrent_processors_and_restart_create_each_semantic_notification_once()
    {
        using var database = new TemporaryCalibrationDatabase();
        DateTimeOffset processingTime;
        await using (var first = await ModuleFixture.CreateAsync(sqliteDatabasePath: database.Path))
        await using (var second = await ModuleFixture.CreateAsync(seedRoles: false, sqliteDatabasePath: database.Path))
        {
            var data = await CalibrationTestData.CreateAsync(first);
            await CalibrationTestData.AddCertificateAsync(first, data, expiresInDays: 60);
            processingTime = first.Clock.GetUtcNow();
            second.ActAs(data.Administrator);
            second.Clock.SetUtc(processingTime);
            using var overlap = new Barrier(2);
            CalibrationReminderProcessor Processor(ModuleFixture fixture) => new(fixture.Db, fixture.Actor,
                fixture.Audit, fixture.Clock, new OverlappingCalibrationProcessing(fixture, overlap));

            var results = await Task.WhenAll(Task.Run(() => Processor(first).ProcessAsync()),
                Task.Run(() => Processor(second).ProcessAsync()));

            Assert.All(results, result => Assert.Equal(0, result.Errors));
            Assert.Equal(2, results.Sum(x => x.NotificationsCreated));
            first.Db.ChangeTracker.Clear();
            Assert.Single(await first.Db.CalibrationNotificationEvents.AsNoTracking().ToListAsync());
            var notifications = await first.Db.Notifications.AsNoTracking().ToListAsync();
            Assert.Equal(2, notifications.Count);
            Assert.Equal(2, notifications.Select(x => x.DeduplicationKey).Distinct().Count());
        }

        await using var restarted = await ModuleFixture.CreateAsync(seedRoles: false, sqliteDatabasePath: database.Path);
        restarted.Clock.SetUtc(processingTime);
        var administrator = await restarted.Db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleAsync(x => x.UserRoles.Any(role => role.Role.Name == "ADMIN"));
        restarted.ActAs(administrator);
        Assert.Equal(0, (await restarted.CalibrationReminders.ProcessAsync()).NotificationsCreated);
        Assert.Equal(2, await restarted.Db.Notifications.CountAsync());
    }

    private sealed class OverlappingCalibrationProcessing(ModuleFixture fixture, Barrier overlap) : IGenerationConcurrency
    {
        private readonly SqliteGenerationConcurrency inner = new(fixture.Db);
        private int entered;
        public void ResetTracking()
        {
            inner.ResetTracking();
            if (Interlocked.Exchange(ref entered, 1) == 0)
                Assert.True(overlap.SignalAndWait(TimeSpan.FromSeconds(20)),
                    "Both processors must capture the same machine before either starts its transaction.");
        }
        public bool IsRetryable(Exception exception) => inner.IsRetryable(exception);
    }

    private sealed class TemporaryCalibrationDatabase : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            $"maintainpro-calibration-test-{Guid.NewGuid():N}.sqlite");
        public void Dispose()
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" }) File.Delete(Path + suffix);
        }
    }
}
