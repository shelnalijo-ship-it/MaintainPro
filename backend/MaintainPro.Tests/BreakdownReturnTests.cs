using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Breakdowns;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class BreakdownReturnTests
{
    [Theory]
    [InlineData(MachineStatus.Operational)]
    [InlineData(MachineStatus.Standby)]
    public async Task Approved_stopped_breakdown_restores_its_actual_safe_prior_state_and_freezes_downtime(MachineStatus previous)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture, machineStatus: previous);
        fixture.Clock.Advance(TimeSpan.FromMinutes(10));
        var submission = await data.ApproveAsync(fixture);
        var closed = await fixture.Breakdowns.GetAsync(data.Breakdown.Id);
        Assert.Equal(BreakdownStatus.CLOSED, closed.Status);
        Assert.True(closed.DowntimeOngoing);
        Assert.Null(closed.ReturnedToServiceAt);
        Assert.Equal(MachineStatus.Breakdown, (await fixture.Db.Machines.SingleAsync()).Status);
        fixture.Clock.Advance(TimeSpan.FromMinutes(20));
        var returned = await fixture.Breakdowns.ReturnToServiceAsync(data.Breakdown.Id, new("Safe functional test completed"));
        Assert.Equal(previous, returned.MachineStatus);
        Assert.Equal(data.Breakdown.Id, Assert.Single(returned.ReturnedBreakdownIds));
        Assert.Equal(fixture.Clock.GetUtcNow().UtcDateTime, returned.ReturnedToServiceAt);
        fixture.Clock.Advance(TimeSpan.FromHours(1));
        var after = await fixture.Breakdowns.GetAsync(data.Breakdown.Id);
        Assert.Equal(30m, after.DowntimeMinutes);
        Assert.False(after.DowntimeOngoing);
        Assert.Equal(previous, after.PreviousMachineStatus);
        Assert.Equal(10m, (await fixture.CorrectiveSubmissions.GetAsync(data.Breakdown.Id, submission.Submission.Id)).Submission.DowntimeMinutes);
        var history = await fixture.Breakdowns.HistoryAsync(data.Breakdown.Id);
        Assert.Equal("Machine.ReturnedToService", history[^1].Action);
        Assert.Equal(returned.ReturnedToServiceAt, (await fixture.Breakdowns.ReturnToServiceAsync(data.Breakdown.Id, new())).ReturnedToServiceAt);
        Assert.Equal(history.Count, (await fixture.Breakdowns.HistoryAsync(data.Breakdown.Id)).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Overlapping_stopped_breakdowns_restore_shared_original_state_only_after_both_are_approved(bool closeSecondFirst)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var first = await BreakdownTestData.CreateAsync(fixture, machineStatus: MachineStatus.Standby);
        fixture.Clock.Advance(TimeSpan.FromMinutes(15));
        var secondDto = await fixture.Breakdowns.ReportAsync(new(first.Machine.Id, BreakdownSeverity.MEDIUM, true, "Separate electrical failure"));
        await fixture.Breakdowns.AssignAsync(secondDto.Id, new(first.Technician.Id));
        var second = first with { Breakdown = secondDto };
        Assert.Equal(MachineStatus.Breakdown, secondDto.PreviousMachineStatus);
        var early = closeSecondFirst ? second : first;
        var late = closeSecondFirst ? first : second;
        await early.ApproveAsync(fixture);
        var blocked = await ModuleFixture.ExpectStatusAsync(409, () => fixture.Breakdowns.ReturnToServiceAsync(early.Breakdown.Id, new()));
        Assert.Contains("unresolved", blocked.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(MachineStatus.Breakdown, (await fixture.Db.Machines.SingleAsync()).Status);
        fixture.Clock.Advance(TimeSpan.FromMinutes(15));
        await late.ApproveAsync(fixture);
        var returned = await fixture.Breakdowns.ReturnToServiceAsync(late.Breakdown.Id, new());
        Assert.Equal(MachineStatus.Standby, returned.MachineStatus);
        Assert.Equal(new[] { first.Breakdown.Id, second.Breakdown.Id }.Order(), returned.ReturnedBreakdownIds.Order());
        var firstRead = await fixture.Breakdowns.GetAsync(first.Breakdown.Id);
        var secondRead = await fixture.Breakdowns.GetAsync(second.Breakdown.Id);
        Assert.Equal(firstRead.ReturnedToServiceAt, secondRead.ReturnedToServiceAt);
        Assert.Equal(30m, firstRead.DowntimeMinutes);
        Assert.Equal(15m, secondRead.DowntimeMinutes);
        Assert.Equal(MachineStatus.Standby, firstRead.PreviousMachineStatus);
        Assert.Equal(MachineStatus.Breakdown, secondRead.PreviousMachineStatus);
    }

    [Theory]
    [InlineData(MachineStatus.Decommissioned)]
    [InlineData(MachineStatus.OutOfService)]
    [InlineData(MachineStatus.UnderMaintenance)]
    public async Task Independent_machine_state_blocks_return_even_after_corrective_approval(MachineStatus independent)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        await data.ApproveAsync(fixture);
        fixture.ActAs(data.Administrator);
        await fixture.Machines.SetStatusAsync(data.Machine.Id, new(independent));
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Breakdowns.ReturnToServiceAsync(data.Breakdown.Id, new()));
        Assert.Equal(independent, (await fixture.Db.Machines.SingleAsync()).Status);
        Assert.Null((await fixture.Breakdowns.GetAsync(data.Breakdown.Id)).ReturnedToServiceAt);
    }

    [Fact]
    public async Task Changing_status_away_and_back_invalidates_stop_provenance_but_ordinary_metadata_edits_do_not()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        await data.ApproveAsync(fixture);
        fixture.ActAs(data.Administrator);
        var machine = await fixture.Db.Machines.SingleAsync();
        var beforeToken = machine.StatusVersion;
        machine.Notes = "Safety inspection notes";
        await fixture.Db.SaveChangesAsync();
        Assert.Equal(beforeToken, machine.StatusVersion);
        await fixture.Machines.SetStatusAsync(data.Machine.Id, new(MachineStatus.OutOfService));
        await fixture.Machines.SetStatusAsync(data.Machine.Id, new(MachineStatus.Breakdown));
        var error = await ModuleFixture.ExpectStatusAsync(409, () => fixture.Breakdowns.ReturnToServiceAsync(data.Breakdown.Id, new()));
        Assert.Contains("independently", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null((await fixture.Breakdowns.GetAsync(data.Breakdown.Id)).ReturnedToServiceAt);
    }

    [Theory]
    [InlineData(MachineStatus.Operational)]
    [InlineData(MachineStatus.Standby)]
    public async Task Generic_machine_status_updates_cannot_bypass_an_unreturned_stopped_breakdown(MachineStatus status)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Machines.SetStatusAsync(data.Machine.Id, new(status)));
        await data.ApproveAsync(fixture);
        fixture.ActAs(data.Administrator);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Machines.SetStatusAsync(data.Machine.Id, new(status)));
        Assert.Equal(MachineStatus.Breakdown, (await fixture.Db.Machines.SingleAsync()).Status);
    }

    [Fact]
    public async Task Return_requires_approval_and_authorized_supervisor_or_manager_and_rejects_non_stopped_reports()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture, stopped: false);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Breakdowns.ReturnToServiceAsync(data.Breakdown.Id, new()));
        await data.ApproveAsync(fixture);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Breakdowns.ReturnToServiceAsync(data.Breakdown.Id, new()));
        fixture.ActAs(data.Technician);
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.Breakdowns.ReturnToServiceAsync(data.Breakdown.Id, new()));
    }

    [Fact]
    public async Task Return_audit_failure_rolls_back_machine_state_and_all_stop_timestamps()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        await data.ApproveAsync(fixture);
        var before = await fixture.Db.BreakdownHistoryEvents.CountAsync();
        var service = new BreakdownService(fixture.Db, fixture.Actor, new FailingAudit(), fixture.Clock,
            new SqliteGenerationConcurrency(fixture.Db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReturnToServiceAsync(data.Breakdown.Id, new()));
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(MachineStatus.Breakdown, (await fixture.Db.Machines.SingleAsync()).Status);
        Assert.Null((await fixture.Db.Breakdowns.SingleAsync()).ReturnedToServiceAt);
        Assert.Equal(before, await fixture.Db.BreakdownHistoryEvents.CountAsync());
    }

    private sealed class FailingAudit : IAuditWriter
    {
        public void Record(string action, string entityType, Guid? entityId, object? oldValues = null,
            object? newValues = null) => throw new InvalidOperationException("Simulated return audit failure.");
    }
}
