using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class BreakdownPersistenceTests
{
    [Theory]
    [InlineData("Description", "Rewritten description")]
    [InlineData("InitialObservation", "Rewritten observation")]
    [InlineData("MachineCode", "REWRITTEN")]
    [InlineData("ReporterName", "Different reporter")]
    public async Task Original_report_identity_and_facts_cannot_be_rewritten(string property, string value)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var breakdown = await SeedBreakdownAsync(fixture);
        fixture.Db.Entry(breakdown).Property(property).CurrentValue = value;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Original_stop_provenance_cannot_be_rewritten()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var breakdown = await SeedBreakdownAsync(fixture);
        breakdown.RestoreMachineStatus = MachineStatus.OutOfService;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Closed_breakdown_allows_one_return_fact_and_rejects_later_rewrites_or_state_changes()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var breakdown = await SeedBreakdownAsync(fixture, BreakdownStatus.CLOSED);
        var returnedAt = fixture.Clock.GetUtcNow().UtcDateTime;
        breakdown.ReturnedToServiceAt = returnedAt;
        breakdown.HistoryVersion++;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        breakdown = await fixture.Db.Breakdowns.SingleAsync();
        Assert.Equal(returnedAt, breakdown.ReturnedToServiceAt);
        breakdown.ReturnedToServiceAt = returnedAt.AddMinutes(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        breakdown = await fixture.Db.Breakdowns.SingleAsync();
        breakdown.Status = BreakdownStatus.IN_PROGRESS;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Machine_status_token_ignores_editorial_changes_and_tracks_intervening_status_or_activation_changes()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var machine = await fixture.SeedMachineAsync();
        var initialToken = machine.StatusVersion;
        machine.Notes = "Editorial update";
        await fixture.Db.SaveChangesAsync();
        Assert.Equal(initialToken, machine.StatusVersion);
        machine.Status = MachineStatus.Breakdown;
        await fixture.Db.SaveChangesAsync();
        Assert.NotEqual(initialToken, machine.StatusVersion);
        var explicitToken = Guid.NewGuid();
        machine.Status = MachineStatus.Operational;
        machine.StatusVersion = explicitToken;
        await fixture.Db.SaveChangesAsync();
        Assert.Equal(explicitToken, machine.StatusVersion);
        machine.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        Assert.NotEqual(explicitToken, machine.StatusVersion);
    }

    [Fact]
    public async Task Snapshot_children_cannot_be_appended_to_a_previously_persisted_submission()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var breakdown = await SeedBreakdownAsync(fixture, BreakdownStatus.AWAITING_APPROVAL);
        var submission = NewSubmission(breakdown);
        fixture.Db.CorrectiveSubmissions.Add(submission);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.CorrectiveSubmissionPartUsages.Add(new CorrectiveSubmissionPartUsage
        {
            CorrectiveSubmissionId = submission.Id, PartName = "Late part", Quantity = 1,
            CreatedByUserId = breakdown.ReportedByUserId, CreatedAt = breakdown.ReportedAt
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Submission_and_its_parts_are_created_atomically_and_remain_immutable()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var breakdown = await SeedBreakdownAsync(fixture, BreakdownStatus.AWAITING_APPROVAL);
        var submission = NewSubmission(breakdown);
        var part = new CorrectiveSubmissionPartUsage
        {
            CorrectiveSubmissionId = submission.Id, PartName = "Seal", Quantity = 1,
            CreatedByUserId = breakdown.ReportedByUserId, CreatedAt = breakdown.ReportedAt
        };
        submission.PartUsages.Add(part);
        fixture.Db.CorrectiveSubmissions.Add(submission);
        await fixture.Db.SaveChangesAsync();
        part.Quantity = 2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        submission = await fixture.Db.CorrectiveSubmissions.SingleAsync();
        submission.RootCause = "Rewritten root cause";
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Pending_outbox_payload_and_processed_delivery_facts_cannot_be_rewritten()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var breakdown = await SeedBreakdownAsync(fixture);
        var intent = new BreakdownNotificationEvent
        {
            BreakdownId = breakdown.Id, NotificationType = NotificationType.BREAKDOWN_REPORTED,
            Title = "Breakdown reported", Message = "Original report notification", DeduplicationKey = "test-outbox"
        };
        fixture.Db.BreakdownNotificationEvents.Add(intent);
        await fixture.Db.SaveChangesAsync();
        intent.Message = "Replacement payload";
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        intent = await fixture.Db.BreakdownNotificationEvents.SingleAsync();
        intent.ProcessedAt = fixture.Clock.GetUtcNow().UtcDateTime;
        await fixture.Db.SaveChangesAsync();
        intent.LastError = "Late rewrite";
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Draft_parts_remain_locked_after_submission_even_when_the_breakdown_is_not_tracked()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var breakdown = await SeedBreakdownAsync(fixture, BreakdownStatus.IN_PROGRESS);
        var part = new CorrectivePartUsage
        {
            BreakdownId = breakdown.Id, PartName = "Bearing", Quantity = 1,
            CreatedByUserId = breakdown.ReportedByUserId
        };
        fixture.Db.CorrectivePartUsages.Add(part);
        await fixture.Db.SaveChangesAsync();
        breakdown.Status = BreakdownStatus.AWAITING_APPROVAL;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        part = await fixture.Db.CorrectivePartUsages.SingleAsync();
        part.Quantity = 3;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public void Breakdown_numbers_and_machine_status_provenance_have_distinct_concurrency_tokens()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=maintainpro_model_tests;Username=model_test").Options);
        var counter = db.Model.FindEntityType(typeof(BreakdownNumberSequence))!;
        Assert.Equal(new[] { "Year" }, counter.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.Equal(typeof(long), counter.FindProperty("LastValue")!.ClrType);
        Assert.True(counter.FindProperty("Version")!.IsConcurrencyToken);
        var machine = db.Model.FindEntityType(typeof(Machine))!;
        Assert.True(machine.FindProperty("Version")!.IsConcurrencyToken);
        Assert.True(machine.FindProperty("StatusVersion")!.IsConcurrencyToken);
    }

    private static async Task<Breakdown> SeedBreakdownAsync(ModuleFixture fixture,
        BreakdownStatus status = BreakdownStatus.REPORTED)
    {
        var user = await fixture.SeedUserAsync("TECHNICIAN", "SUPERVISOR");
        var machine = await fixture.SeedMachineAsync(user.Id, user.Id);
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var breakdown = new Breakdown
        {
            BreakdownNumber = "BD-2026-0001", MachineId = machine.Id, MachineCode = machine.MachineCode,
            MachineName = machine.Name, ReportedByUserId = user.Id, ReporterEmployeeId = user.EmployeeId,
            ReporterName = user.FirstName, ReportedAt = now, Severity = BreakdownSeverity.HIGH,
            MachineStopped = true, Description = "Reported failure", SupervisorId = user.Id,
            SupervisorEmployeeId = user.EmployeeId, SupervisorName = user.FirstName,
            AssignedTechnicianId = user.Id, PreviousMachineStatus = MachineStatus.Operational,
            RestoreMachineStatus = MachineStatus.Operational, MachineStatusVersionAtStop = machine.StatusVersion,
            Status = status, ClosedAt = status == BreakdownStatus.CLOSED ? now : null
        };
        fixture.Db.Breakdowns.Add(breakdown);
        await fixture.Db.SaveChangesAsync();
        return breakdown;
    }

    private static CorrectiveSubmission NewSubmission(Breakdown breakdown) => new()
    {
        BreakdownId = breakdown.Id, VersionNumber = 1, TechnicianId = breakdown.ReportedByUserId,
        TechnicianEmployeeId = breakdown.ReporterEmployeeId, TechnicianName = breakdown.ReporterName,
        RootCause = "Worn seal", CorrectiveAction = "Replaced seal", StartedAt = breakdown.ReportedAt,
        CompletedAt = breakdown.ReportedAt, SubmittedAt = breakdown.ReportedAt
    };
}
