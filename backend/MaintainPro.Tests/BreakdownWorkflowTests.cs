using System.Text.Json;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Breakdowns;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class BreakdownWorkflowTests
{
    [Fact]
    public async Task Rejected_correction_preserves_v1_parts_evidence_and_time_facts_while_v2_is_approved()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);
        fixture.Clock.Advance(TimeSpan.FromMinutes(10));
        await fixture.CorrectiveExecution.StartAsync(data.Breakdown.Id);
        await fixture.CorrectiveExecution.SaveAsync(data.Breakdown.Id, new("Worn bearing", "Changed bearing", "Original repair"));
        var part = await fixture.CorrectiveExecution.AddPartAsync(data.Breakdown.Id, new("Bearing", 1m, "BR-1"));
        var attachment = await BreakdownTestData.UploadAsync(fixture, data.Breakdown.Id);
        fixture.Clock.Advance(TimeSpan.FromMinutes(20));
        await fixture.CorrectiveExecution.CompleteAsync(data.Breakdown.Id);
        fixture.Clock.Advance(TimeSpan.FromMinutes(10));
        var first = await fixture.CorrectiveSubmissions.SubmitAsync(data.Breakdown.Id);
        Assert.Equal(1, first.Submission.VersionNumber);
        Assert.Equal(20m, first.Submission.DurationMinutes);
        Assert.Equal(40m, first.Submission.DowntimeMinutes);
        fixture.Clock.Advance(TimeSpan.FromMinutes(60));
        fixture.ActAs(data.Supervisor);
        await fixture.CorrectiveReviews.RejectAsync(data.Breakdown.Id, new(first.Submission.Id, "Inspect shaft alignment too"));
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        fixture.ActAs(data.Technician);
        await fixture.CorrectiveExecution.ResumeAsync(data.Breakdown.Id);
        await fixture.CorrectiveExecution.SaveAsync(data.Breakdown.Id, new("Worn bearing and misalignment", "Replaced bearings and aligned shaft", "Final repair"));
        await fixture.CorrectiveExecution.UpdatePartAsync(data.Breakdown.Id, part.Id, new("Bearing", 2m, "BR-1"));
        await fixture.BreakdownEvidence.DeleteAsync(data.Breakdown.Id, attachment.Id);
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        await fixture.CorrectiveExecution.CompleteAsync(data.Breakdown.Id);
        var second = await fixture.CorrectiveSubmissions.SubmitAsync(data.Breakdown.Id);
        Assert.Equal(2, second.Submission.VersionNumber);
        Assert.Equal(25m, second.Submission.DurationMinutes);
        Assert.Equal(110m, second.Submission.DowntimeMinutes);
        fixture.ActAs(data.Supervisor);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.CorrectiveReviews.ApproveAsync(data.Breakdown.Id, new(first.Submission.Id)));
        var approved = await fixture.CorrectiveReviews.ApproveAsync(data.Breakdown.Id, new(second.Submission.Id, "Alignment verified"));
        Assert.Equal(CorrectiveReviewDecision.APPROVED, approved.Decision);
        Assert.Equal(second.Submission.Id, approved.CorrectiveSubmissionId);
        var original = await fixture.CorrectiveSubmissions.GetAsync(data.Breakdown.Id, first.Submission.Id);
        Assert.Equal("Worn bearing", original.RootCause);
        Assert.Equal("Changed bearing", original.CorrectiveAction);
        Assert.Equal("Original repair", original.Comments);
        Assert.Equal(1m, Assert.Single(original.Parts).Quantity);
        Assert.Equal(attachment.FileId, Assert.Single(original.Attachments).FileId);
        Assert.Equal(CorrectiveReviewDecision.REJECTED, original.Submission.Review!.Decision);
        Assert.Equal(40m, original.Submission.DowntimeMinutes);
        Assert.Equal(2m, Assert.Single(second.Parts).Quantity);
        Assert.Empty(second.Attachments);
        Assert.Equal(2, (await fixture.CorrectiveSubmissions.ListAsync(data.Breakdown.Id)).Count);
        Assert.DoesNotContain("StorageKey", JsonSerializer.Serialize(original));
        Assert.Equal(BreakdownStatus.CLOSED, (await fixture.Breakdowns.GetAsync(data.Breakdown.Id)).Status);
        Assert.Equal(MachineStatus.Breakdown, (await fixture.Db.Machines.SingleAsync()).Status);
        var history = await fixture.Breakdowns.HistoryAsync(data.Breakdown.Id);
        Assert.Equal(Enumerable.Range(1, history.Count), history.Select(x => x.SequenceNumber));
        Assert.Contains(history, x => x.Action == "Corrective.Submitted" && x.SubmissionVersion == 1);
        Assert.Contains(history, x => x.Action == "Corrective.Submitted" && x.SubmissionVersion == 2);
        var notices = await fixture.Db.Notifications.ToListAsync();
        Assert.Equal(2, notices.Count(x => x.NotificationType == NotificationType.CORRECTIVE_SUBMITTED));
        Assert.Equal(data.Technician.Id, Assert.Single(notices, x => x.NotificationType == NotificationType.CORRECTIVE_REJECTED).UserId);
        Assert.Equal(data.Technician.Id, Assert.Single(notices, x => x.NotificationType == NotificationType.CORRECTIVE_APPROVED).UserId);
        await fixture.AssertAuditHasNoSecretsAsync(fixture.Password);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("Cause", null)]
    [InlineData(null, "Action")]
    [InlineData(" ", "Action")]
    public async Task Completion_requires_root_cause_and_corrective_action(string? cause, string? action)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);
        await fixture.CorrectiveExecution.StartAsync(data.Breakdown.Id);
        await fixture.CorrectiveExecution.SaveAsync(data.Breakdown.Id, new(cause, action));
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.CorrectiveExecution.CompleteAsync(data.Breakdown.Id));
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.CorrectiveSubmissions.SubmitAsync(data.Breakdown.Id));
        Assert.Empty(await fixture.Db.CorrectiveSubmissions.ToListAsync());
    }

    [Fact]
    public async Task Changed_completed_draft_requires_completion_again_and_submitted_draft_rejects_edits()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);
        await fixture.CorrectiveExecution.StartAsync(data.Breakdown.Id);
        await fixture.CorrectiveExecution.SaveAsync(data.Breakdown.Id, new("Cause", "Repair"));
        await fixture.CorrectiveExecution.CompleteAsync(data.Breakdown.Id);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.CorrectiveExecution.CompleteAsync(data.Breakdown.Id));
        await fixture.CorrectiveExecution.SaveAsync(data.Breakdown.Id, new("Cause", "Repair", "Additional check"));
        Assert.False((await fixture.CorrectiveExecution.GetAsync(data.Breakdown.Id)).IsComplete);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.CorrectiveSubmissions.SubmitAsync(data.Breakdown.Id));
        await fixture.CorrectiveExecution.CompleteAsync(data.Breakdown.Id);
        await fixture.CorrectiveSubmissions.SubmitAsync(data.Breakdown.Id);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.CorrectiveSubmissions.SubmitAsync(data.Breakdown.Id));
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.CorrectiveExecution.SaveAsync(data.Breakdown.Id, new("Changed", "Changed")));
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.CorrectiveExecution.ResumeAsync(data.Breakdown.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Part_quantity_must_be_positive_for_add_and_update(decimal quantity)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);
        await fixture.CorrectiveExecution.StartAsync(data.Breakdown.Id);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.CorrectiveExecution.AddPartAsync(data.Breakdown.Id, new("Bearing", quantity)));
        var part = await fixture.CorrectiveExecution.AddPartAsync(data.Breakdown.Id, new("Bearing", 1));
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.CorrectiveExecution.UpdatePartAsync(data.Breakdown.Id, part.Id, new("Bearing", quantity)));
        Assert.Equal(1m, Assert.Single((await fixture.CorrectiveExecution.GetAsync(data.Breakdown.Id)).Parts).Quantity);
    }

    [Fact]
    public async Task Execution_and_review_authorization_and_exact_version_guards_are_enforced_by_services()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.CorrectiveExecution.StartAsync(data.Breakdown.Id));
        var unrelated = await fixture.SeedUserAsync("TECHNICIAN", "SUPERVISOR");
        fixture.ActAs(unrelated);
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.CorrectiveExecution.StartAsync(data.Breakdown.Id));
        var submission = await data.SubmitAsync(fixture);
        fixture.ActAs(unrelated);
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.CorrectiveReviews.ApproveAsync(data.Breakdown.Id, new(submission.Submission.Id)));
        fixture.ActAs(data.Administrator);
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.CorrectiveReviews.ApproveAsync(data.Breakdown.Id, new(submission.Submission.Id)));
        fixture.ActAs(data.Supervisor);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.CorrectiveReviews.RejectAsync(data.Breakdown.Id, new(submission.Submission.Id)));
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.CorrectiveReviews.ApproveAsync(data.Breakdown.Id, new(Guid.NewGuid())));
        await fixture.CorrectiveReviews.ApproveAsync(data.Breakdown.Id, new(submission.Submission.Id));
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.CorrectiveReviews.ApproveAsync(data.Breakdown.Id, new(submission.Submission.Id)));
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.CorrectiveReviews.RejectAsync(data.Breakdown.Id, new(submission.Submission.Id, "Too late")));
    }

    [Fact]
    public async Task A_technician_with_supervisor_role_cannot_review_their_own_correction()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture, assigned: false);
        var both = await fixture.SeedUserAsync("TECHNICIAN", "SUPERVISOR");
        await fixture.Breakdowns.AssignAsync(data.Breakdown.Id, new(both.Id, both.Id));
        fixture.ActAs(both);
        await fixture.CorrectiveExecution.StartAsync(data.Breakdown.Id);
        await fixture.CorrectiveExecution.SaveAsync(data.Breakdown.Id, new("Cause", "Repair"));
        await fixture.CorrectiveExecution.CompleteAsync(data.Breakdown.Id);
        var submission = await fixture.CorrectiveSubmissions.SubmitAsync(data.Breakdown.Id);
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.CorrectiveReviews.ApproveAsync(data.Breakdown.Id, new(submission.Submission.Id)));
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.CorrectiveReviews.RejectAsync(data.Breakdown.Id, new(submission.Submission.Id, "Self review")));
    }

    [Fact]
    public async Task Submission_audit_failure_cannot_partially_persist_immutable_facts_or_notifications()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);
        await fixture.CorrectiveExecution.StartAsync(data.Breakdown.Id);
        await fixture.CorrectiveExecution.SaveAsync(data.Breakdown.Id, new("Cause", "Repair"));
        await fixture.CorrectiveExecution.CompleteAsync(data.Breakdown.Id);
        var notices = await fixture.Db.Notifications.CountAsync();
        var events = await fixture.Db.BreakdownNotificationEvents.CountAsync();
        var history = await fixture.Db.BreakdownHistoryEvents.CountAsync();
        var service = new CorrectiveSubmissionService(fixture.Db, fixture.Actor, new FailingAudit(), fixture.Clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAsync(data.Breakdown.Id));
        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Db.CorrectiveSubmissions.ToListAsync());
        Assert.Equal(0, (await fixture.Db.Breakdowns.SingleAsync()).SubmissionVersion);
        Assert.Equal(BreakdownStatus.IN_PROGRESS, (await fixture.Db.Breakdowns.SingleAsync()).Status);
        Assert.Equal(notices, await fixture.Db.Notifications.CountAsync());
        Assert.Equal(events, await fixture.Db.BreakdownNotificationEvents.CountAsync());
        Assert.Equal(history, await fixture.Db.BreakdownHistoryEvents.CountAsync());
    }

    private sealed class FailingAudit : IAuditWriter
    {
        public void Record(string action, string entityType, Guid? entityId, object? oldValues = null,
            object? newValues = null) => throw new InvalidOperationException("Simulated corrective audit failure.");
    }
}
