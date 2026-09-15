using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Breakdowns;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class BreakdownReportingTests
{
    [Fact]
    public async Task Report_captures_machine_reporter_status_and_UTC_facts_without_assigning_corrective_work()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture, assigned: false);
        var reported = data.Breakdown;
        Assert.Matches(@"^BD-\d{4}-0001$", reported.BreakdownNumber);
        Assert.Equal(BreakdownStatus.REPORTED, reported.Status);
        Assert.Null(reported.AssignedTechnicianId);
        Assert.Equal(data.Administrator.Id, reported.ReportedByUserId);
        Assert.Equal(data.Supervisor.Id, reported.SupervisorId);
        Assert.Equal(fixture.Clock.GetUtcNow().UtcDateTime, reported.ReportedAt);
        Assert.Equal(DateTimeKind.Utc, reported.ReportedAt.Kind);
        Assert.Equal(MachineStatus.Operational, reported.PreviousMachineStatus);
        Assert.Equal(MachineStatus.Breakdown, (await fixture.Db.Machines.SingleAsync()).Status);
        Assert.Empty(await fixture.Db.CorrectiveActionDrafts.ToListAsync());
        Assert.Contains(await fixture.Breakdowns.HistoryAsync(reported.Id), x => x.Action == "Breakdown.Reported");
    }

    [Theory]
    [InlineData(MachineStatus.Operational)]
    [InlineData(MachineStatus.Standby)]
    [InlineData(MachineStatus.OutOfService)]
    [InlineData(MachineStatus.Decommissioned)]
    public async Task Non_stopped_reports_preserve_existing_machine_state_and_have_zero_downtime(MachineStatus status)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture, stopped: false, assigned: false, machineStatus: status);
        fixture.Clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(status, (await fixture.Db.Machines.SingleAsync()).Status);
        Assert.Equal(0m, (await fixture.Breakdowns.GetAsync(data.Breakdown.Id)).DowntimeMinutes);
        Assert.False((await fixture.Breakdowns.GetAsync(data.Breakdown.Id)).DowntimeOngoing);
    }

    [Fact]
    public async Task Stopped_report_does_not_overwrite_decommissioned_machine_status()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture, assigned: false, machineStatus: MachineStatus.Decommissioned);
        Assert.Equal(MachineStatus.Decommissioned, data.Breakdown.PreviousMachineStatus);
        Assert.Equal(MachineStatus.Decommissioned, (await fixture.Db.Machines.SingleAsync()).Status);
    }

    [Fact]
    public async Task Critical_report_routes_to_supervisor_and_active_managers_once_per_recipient()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var administrator = await fixture.AsAdminAsync();
        var supervisor = await fixture.SeedUserAsync("SUPERVISOR", "MANAGER");
        var manager = await fixture.SeedUserAsync("MANAGER", "ADMIN");
        var inactive = await fixture.SeedUserAsync("MANAGER");
        inactive.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        var machine = await fixture.SeedMachineAsync(supervisor: supervisor.Id);
        var report = await fixture.Breakdowns.ReportAsync(new(machine.Id, BreakdownSeverity.CRITICAL, true, "Critical motor failure"));
        var notices = await fixture.Db.Notifications.Where(x => x.NotificationType == NotificationType.BREAKDOWN_CRITICAL).ToListAsync();
        Assert.Equal(new[] { supervisor.Id, manager.Id }.Order(), notices.Select(x => x.UserId).Order());
        Assert.All(notices, x => Assert.Equal(report.Id, x.EntityId));
        Assert.DoesNotContain(notices, x => x.UserId == administrator.Id || x.UserId == inactive.Id);
        var count = await fixture.Db.Notifications.CountAsync();
        Assert.Equal(0, (await fixture.BreakdownNotifications.ProcessPendingAsync()).NotificationsCreated);
        Assert.Equal(count, await fixture.Db.Notifications.CountAsync());
        Assert.Equal(count * 3, await fixture.Db.NotificationDeliveryAttempts.CountAsync());
    }

    [Theory]
    [InlineData("machine", 404)]
    [InlineData("description", 400)]
    [InlineData("severity", 400)]
    [InlineData("supervisor", 400)]
    public async Task Invalid_reporting_facts_fail_without_allocating_a_number_or_changing_machine(string invalid, int status)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var supervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var machine = await fixture.SeedMachineAsync(supervisor: supervisor.Id);
        var request = new BreakdownReportRequest(machine.Id, BreakdownSeverity.HIGH, true, "Unexpected vibration");
        request = invalid switch
        {
            "machine" => request with { MachineId = Guid.NewGuid() },
            "description" => request with { Description = " " },
            "severity" => request with { Severity = (BreakdownSeverity)900 },
            _ => request with { SupervisorId = Guid.NewGuid() }
        };
        await ModuleFixture.ExpectStatusAsync(status, () => fixture.Breakdowns.ReportAsync(request));
        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Db.Breakdowns.ToListAsync());
        Assert.Empty(await fixture.Db.BreakdownNumberSequences.ToListAsync());
        Assert.Equal(MachineStatus.Operational, (await fixture.Db.Machines.SingleAsync()).Status);
    }

    [Fact]
    public async Task Report_uses_existing_machine_visibility_and_supervisor_override_permissions()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var technician = await fixture.SeedUserAsync("TECHNICIAN");
        var otherTechnician = await fixture.SeedUserAsync("TECHNICIAN");
        var supervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var otherSupervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var machine = await fixture.SeedMachineAsync(technician.Id, supervisor.Id);
        fixture.ActAs(otherTechnician);
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Breakdowns.ReportAsync(new(machine.Id, BreakdownSeverity.LOW, false, "Noise")));
        fixture.ActAs(technician);
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.Breakdowns.ReportAsync(new(machine.Id, BreakdownSeverity.LOW, false, "Noise", SupervisorId: otherSupervisor.Id)));
        var reported = await fixture.Breakdowns.ReportAsync(new(machine.Id, BreakdownSeverity.LOW, false, "Noise"));
        Assert.Equal(technician.Id, reported.ReportedByUserId);
        Assert.Equal(supervisor.Id, reported.SupervisorId);
    }

    [Fact]
    public async Task Reporting_audit_failure_rolls_back_number_machine_history_and_notifications()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var supervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var machine = await fixture.SeedMachineAsync(supervisor: supervisor.Id);
        var service = new BreakdownService(fixture.Db, fixture.Actor, new FailingAudit(), fixture.Clock,
            new SqliteGenerationConcurrency(fixture.Db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReportAsync(new(machine.Id,
            BreakdownSeverity.CRITICAL, true, "Stop immediately")));
        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Db.Breakdowns.ToListAsync());
        Assert.Empty(await fixture.Db.BreakdownNumberSequences.ToListAsync());
        Assert.Empty(await fixture.Db.BreakdownHistoryEvents.ToListAsync());
        Assert.Empty(await fixture.Db.BreakdownNotificationEvents.ToListAsync());
        Assert.Empty(await fixture.Db.Notifications.ToListAsync());
        Assert.Equal(MachineStatus.Operational, (await fixture.Db.Machines.SingleAsync()).Status);
    }

    private sealed class FailingAudit : IAuditWriter
    {
        public void Record(string action, string entityType, Guid? entityId, object? oldValues = null,
            object? newValues = null) => throw new InvalidOperationException("Simulated report audit failure.");
    }
}
