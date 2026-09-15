using System.Text.Json;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Execution;
using MaintainPro.Application.Planning;
using MaintainPro.Application.Reviews;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class ExecutionSubmissionTests
{
    [Fact]
    public async Task Submission_requires_completion_and_a_satisfied_mandatory_answer_without_partial_rows()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Submissions.SubmitAsync(data.WorkOrder.Id));
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        var missing = await ModuleFixture.ExpectStatusAsync(400, () => fixture.Submissions.SubmitAsync(data.WorkOrder.Id));
        Assert.Contains("Confirm inspection", missing.Message);
        Assert.Empty(await fixture.Db.WorkOrderSubmissions.ToListAsync());
        Assert.Equal(WorkOrderLifecycleStatus.IN_PROGRESS, data.WorkOrder.LifecycleStatus);
        Assert.Null(data.WorkOrder.SubmittedAt);
    }

    [Fact]
    public async Task Required_comment_is_validated_against_the_immutable_generated_definition()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture, commentRequired: true);
        await ReadyAsync(fixture, data);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Submissions.SubmitAsync(data.WorkOrder.Id));
        await fixture.Execution.SaveCommentsAsync(data.WorkOrder.Id, new ExecutionCommentsRequest("Inspection completed"));
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        var submission = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        Assert.Equal("Inspection completed", submission.OverallComments);
    }

    [Theory]
    [InlineData(ChecklistResponseType.BOOLEAN)]
    [InlineData(ChecklistResponseType.PASS_FAIL)]
    [InlineData(ChecklistResponseType.NUMBER)]
    public async Task False_fail_and_out_of_range_readings_are_valid_answers_not_automatic_submission_blockers(ChecklistResponseType type)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var checklist = new ChecklistWriteRequest("Result semantics", [new ChecklistItemWriteRequest(1, "Result", type,
            MinimumValue: type == ChecklistResponseType.NUMBER ? 1m : null,
            MaximumValue: type == ChecklistResponseType.NUMBER ? 10m : null)]);
        var data = await ExecutionTestData.CreateAsync(fixture, checklist);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        var answer = type switch
        {
            ChecklistResponseType.BOOLEAN => new ChecklistResultRequest(data.Item.Id, BooleanValue: false),
            ChecklistResponseType.PASS_FAIL => new ChecklistResultRequest(data.Item.Id, PassFailValue: PassFailResult.FAIL),
            _ => new ChecklistResultRequest(data.Item.Id, NumericValue: -2m)
        };
        await fixture.Execution.SaveChecklistResultsAsync(data.WorkOrder.Id, new ChecklistResultsRequest([answer]));
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);

        var submitted = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);

        Assert.Equal(1, submitted.Submission.VersionNumber);
        if (type == ChecklistResponseType.NUMBER)
        {
            Assert.Equal(-2m, Assert.Single(submitted.ChecklistResults).NumericValue);
            Assert.Equal(NumericReadingStatus.BELOW, Assert.Single(submitted.ChecklistResults).NumericRangeStatus);
        }
    }

    [Fact]
    public async Task Optional_answers_may_be_omitted_but_confirmation_must_be_explicitly_true()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture, new ChecklistWriteRequest("Optional text",
        [
            new ChecklistItemWriteRequest(1, "Confirm", ChecklistResponseType.CONFIRMATION),
            new ChecklistItemWriteRequest(2, "Optional notes", ChecklistResponseType.TEXT, IsMandatory: false)
        ]));
        var item = data.WorkOrder.Definition.Items.Single(x => x.ResponseType == ChecklistResponseType.CONFIRMATION);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        await fixture.Execution.SaveChecklistResultsAsync(data.WorkOrder.Id,
            new ChecklistResultsRequest([new ChecklistResultRequest(item.Id, ConfirmationValue: false)]));
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Submissions.SubmitAsync(data.WorkOrder.Id));
        await fixture.Execution.SaveChecklistResultsAsync(data.WorkOrder.Id,
            new ChecklistResultsRequest([new ChecklistResultRequest(item.Id, ConfirmationValue: true)]));
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        Assert.Equal(1, (await fixture.Submissions.SubmitAsync(data.WorkOrder.Id)).Submission.VersionNumber);
    }

    [Fact]
    public async Task Submit_is_atomic_allocates_version_one_and_rejects_retry_and_draft_changes()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await ReadyAsync(fixture, data);
        var completedAt = data.WorkOrder.CompletedAt;
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));

        var result = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);

        Assert.Equal(1, result.Submission.VersionNumber);
        Assert.Equal(data.Technician.Id, result.Submission.SubmittedByUserId);
        Assert.Equal(fixture.Clock.GetUtcNow().UtcDateTime, result.Submission.SubmittedAt);
        Assert.Equal(completedAt, result.Submission.CompletedAt);
        Assert.Equal(WorkOrderLifecycleStatus.AWAITING_APPROVAL, data.WorkOrder.LifecycleStatus);
        Assert.Null(data.WorkOrder.ApprovedAt);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Submissions.SubmitAsync(data.WorkOrder.Id));
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Execution.SaveCommentsAsync(data.WorkOrder.Id, new("Cannot edit")));
        Assert.Equal(1, await fixture.Db.WorkOrderSubmissions.CountAsync());
    }

    [Fact]
    public async Task Submission_rolls_back_version_state_and_snapshots_when_audit_fails()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await ReadyAsync(fixture, data);
        var failing = new WorkOrderSubmissionService(fixture.Db, fixture.Actor, new FailingAudit(), fixture.Clock);

        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.SubmitAsync(data.WorkOrder.Id));

        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Db.WorkOrderSubmissions.ToListAsync());
        Assert.Empty(await fixture.Db.WorkOrderSubmissionChecklistResults.ToListAsync());
        var persisted = await fixture.Db.WorkOrders.SingleAsync(x => x.Id == data.WorkOrder.Id);
        Assert.Equal(WorkOrderLifecycleStatus.IN_PROGRESS, persisted.LifecycleStatus);
        Assert.Null(persisted.SubmittedAt);
        Assert.Equal(1, (await fixture.Submissions.SubmitAsync(data.WorkOrder.Id)).Submission.VersionNumber);
    }

    [Fact]
    public async Task Rejection_resume_resubmission_and_approval_preserve_every_submission_and_exact_decision()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        await fixture.Execution.SaveChecklistResultsAsync(data.WorkOrder.Id,
            new ChecklistResultsRequest([new ChecklistResultRequest(data.Item.Id, ConfirmationValue: true, Comment: "Version one answer")]));
        await fixture.Execution.SaveCommentsAsync(data.WorkOrder.Id, new ExecutionCommentsRequest("Version one comments", "Original observations"));
        var part = await fixture.Execution.AddPartAsync(data.WorkOrder.Id, new PartUsageRequest("Bearing A", 1m, "BR-A", "Original part"));
        var defect = await fixture.Execution.AddDefectAsync(data.WorkOrder.Id, new DefectRequest("Original defect", "Original description", "HIGH", true));
        fixture.Clock.Advance(TimeSpan.FromMinutes(10));
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        var first = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        fixture.ActAs(data.Supervisor);
        await fixture.Reviews.RejectAsync(data.WorkOrder.Id, new WorkOrderReviewRequest(first.Submission.Id, "Please correct the reading"));
        var frozenFirst = await fixture.Submissions.GetAsync(data.WorkOrder.Id, first.Submission.Id);
        var firstJson = JsonSerializer.Serialize(frozenFirst);
        Assert.Equal(WorkOrderReviewDecision.REJECTED, frozenFirst.Submission.Review!.Decision);
        Assert.Equal(first.Submission.Id, frozenFirst.Submission.Review.WorkOrderSubmissionId);

        fixture.Clock.Advance(TimeSpan.FromHours(3));
        fixture.ActAs(data.Technician);
        await fixture.Execution.ResumeAsync(data.WorkOrder.Id);
        await fixture.Execution.SaveCommentsAsync(data.WorkOrder.Id, new ExecutionCommentsRequest("Corrected comments", "New observations"));
        await fixture.Execution.SaveChecklistResultsAsync(data.WorkOrder.Id,
            new ChecklistResultsRequest([new ChecklistResultRequest(data.Item.Id, ConfirmationValue: true, Comment: "Version two answer")]));
        await fixture.Execution.UpdatePartAsync(data.WorkOrder.Id, part.Id, new PartUsageRequest("Bearing B", 2m, "BR-B"));
        await fixture.Execution.UpdateDefectAsync(data.WorkOrder.Id, defect.Id, new DefectRequest("Updated defect", "Updated description", "LOW"));
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        var second = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        Assert.Equal(2, second.Submission.VersionNumber);
        Assert.Equal(15m, second.Submission.DurationMinutes);
        Assert.Equal("Corrected comments", second.OverallComments);
        Assert.Equal("Version two answer", Assert.Single(second.ChecklistResults).Comment);
        Assert.Equal("Bearing B", Assert.Single(second.PartUsages).PartName);
        Assert.Equal("Updated defect", Assert.Single(second.Defects).Title);

        fixture.ActAs(data.Supervisor);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Reviews.ApproveAsync(data.WorkOrder.Id,
            new WorkOrderReviewRequest(first.Submission.Id, "Stale version")));
        await fixture.Reviews.ApproveAsync(data.WorkOrder.Id, new WorkOrderReviewRequest(second.Submission.Id, "Accepted"));
        var approved = await fixture.Submissions.GetAsync(data.WorkOrder.Id, second.Submission.Id);
        Assert.Equal(WorkOrderReviewDecision.APPROVED, approved.Submission.Review!.Decision);
        Assert.Equal(second.Submission.Id, approved.Submission.Review.WorkOrderSubmissionId);
        Assert.Equal(firstJson, JsonSerializer.Serialize(await fixture.Submissions.GetAsync(data.WorkOrder.Id, first.Submission.Id)));
        Assert.Equal(2, (await fixture.Submissions.ListAsync(data.WorkOrder.Id)).Count);
        Assert.Equal(WorkOrderLifecycleStatus.APPROVED, data.WorkOrder.LifecycleStatus);
        Assert.Equal(second.Submission.SubmittedAt, data.WorkOrder.SubmittedAt);
        Assert.Equal(second.Submission.CompletedAt, data.WorkOrder.CompletedAt);
        Assert.NotNull(data.WorkOrder.ApprovedAt);

        fixture.ActAs(data.Technician);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Execution.ResumeAsync(data.WorkOrder.Id));
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Execution.UpdatePartAsync(data.WorkOrder.Id, part.Id, new("Cannot change", 1m)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rejection_requires_a_reason(string? reason)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await ReadyAsync(fixture, data);
        var submission = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        fixture.ActAs(data.Supervisor);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Reviews.RejectAsync(data.WorkOrder.Id,
            new WorkOrderReviewRequest(submission.Submission.Id, reason)));
        Assert.Equal(WorkOrderLifecycleStatus.AWAITING_APPROVAL, data.WorkOrder.LifecycleStatus);
        Assert.Null((await fixture.Submissions.GetAsync(data.WorkOrder.Id, submission.Submission.Id)).Submission.Review);
    }

    [Theory]
    [InlineData("SUPERVISOR", 404)]
    [InlineData("MANAGER", 403)]
    [InlineData("ADMIN", 403)]
    public async Task Reviews_require_the_assigned_supervisor_without_a_manager_override(string role, int status)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await ReadyAsync(fixture, data);
        var submission = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        fixture.ActAs(await fixture.SeedUserAsync(role));
        await ModuleFixture.ExpectStatusAsync(status, () => fixture.Reviews.ApproveAsync(data.WorkOrder.Id,
            new WorkOrderReviewRequest(submission.Submission.Id)));
    }

    [Fact]
    public async Task Technician_with_supervisor_role_cannot_review_their_own_execution()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        var supervisorRole = await fixture.Db.Roles.SingleAsync(x => x.Name == "SUPERVISOR");
        data.Technician.UserRoles.Add(new UserRole { UserId = data.Technician.Id, RoleId = supervisorRole.Id, Role = supervisorRole });
        data.WorkOrder.SupervisorId = data.Technician.Id;
        await fixture.Db.SaveChangesAsync();
        fixture.ActAs(data.Technician);
        await ReadyAsync(fixture, data);
        var submission = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.Reviews.ApproveAsync(data.WorkOrder.Id,
            new WorkOrderReviewRequest(submission.Submission.Id)));
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.Reviews.RejectAsync(data.WorkOrder.Id,
            new WorkOrderReviewRequest(submission.Submission.Id, "Self rejection")));
    }

    [Fact]
    public async Task Approval_retries_are_rejected_and_review_failure_rolls_back_atomically()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await ReadyAsync(fixture, data);
        var submission = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        fixture.ActAs(data.Supervisor);
        var failing = new WorkOrderReviewService(fixture.Db, fixture.Actor, new FailingAudit(), fixture.Clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.ApproveAsync(data.WorkOrder.Id, new(submission.Submission.Id)));
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(WorkOrderLifecycleStatus.AWAITING_APPROVAL, (await fixture.Db.WorkOrders.SingleAsync()).LifecycleStatus);
        Assert.Null((await fixture.Submissions.GetAsync(data.WorkOrder.Id, submission.Submission.Id)).Submission.Review);
        await fixture.Reviews.ApproveAsync(data.WorkOrder.Id, new(submission.Submission.Id));
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Reviews.ApproveAsync(data.WorkOrder.Id, new(submission.Submission.Id)));
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Reviews.RejectAsync(data.WorkOrder.Id, new(submission.Submission.Id, "Already final")));
    }

    internal static async Task ReadyAsync(ModuleFixture fixture, ExecutionTestData data)
    {
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        await fixture.Execution.SaveChecklistResultsAsync(data.WorkOrder.Id,
            new ChecklistResultsRequest([new ChecklistResultRequest(data.Item.Id, ConfirmationValue: true)]));
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
    }

    private sealed class FailingAudit : IAuditWriter
    {
        public void Record(string action, string entityType, Guid? entityId, object? oldValues = null,
            object? newValues = null) => throw new InvalidOperationException("Simulated audit persistence failure.");
    }
}
