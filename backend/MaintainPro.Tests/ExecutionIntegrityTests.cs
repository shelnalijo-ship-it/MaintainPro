using MaintainPro.Application.Audit;
using MaintainPro.Application.Execution;
using MaintainPro.Application.Reviews;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class ExecutionIntegrityTests
{
    [Fact]
    public async Task A_stale_draft_write_cannot_overwrite_a_newer_draft_or_persist_its_audit()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        await using var stale = NewContext(fixture);
        await stale.WorkOrders.SingleAsync();
        await stale.WorkOrderExecutions.SingleAsync();
        var staleService = new WorkOrderExecutionService(stale, fixture.Actor,
            new AuditWriter(stale, fixture.Actor, fixture.Clock), fixture.Clock);

        await fixture.Execution.SaveCommentsAsync(data.WorkOrder.Id, new("Newer winning draft"));
        var auditCount = await fixture.Db.AuditLogs.CountAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            staleService.SaveCommentsAsync(data.WorkOrder.Id, new("Stale losing draft")));

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal("Newer winning draft", (await fixture.Db.WorkOrderExecutions.SingleAsync()).OverallComments);
        Assert.Equal(auditCount, await fixture.Db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task A_stale_submission_request_cannot_create_a_second_version_or_duplicate_history()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await ExecutionSubmissionTests.ReadyAsync(fixture, data);
        await using var stale = NewContext(fixture);
        await stale.WorkOrders.Include(x => x.Definition).ThenInclude(x => x.Items).SingleAsync();
        await stale.WorkOrderExecutions.SingleAsync();
        var staleService = new WorkOrderSubmissionService(stale, fixture.Actor,
            new AuditWriter(stale, fixture.Actor, fixture.Clock), fixture.Clock);
        var first = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        var auditCount = await fixture.Db.AuditLogs.CountAsync();
        var historyCount = await fixture.Db.WorkOrderHistoryEvents.CountAsync();

        var failure = await Record.ExceptionAsync(() => staleService.SubmitAsync(data.WorkOrder.Id));

        Assert.IsAssignableFrom<DbUpdateException>(failure);
        fixture.Db.ChangeTracker.Clear();
        var submission = Assert.Single(await fixture.Db.WorkOrderSubmissions.ToListAsync());
        Assert.Equal(first.Submission.Id, submission.Id);
        Assert.Equal(1, submission.VersionNumber);
        Assert.Equal(auditCount, await fixture.Db.AuditLogs.CountAsync());
        Assert.Equal(historyCount, await fixture.Db.WorkOrderHistoryEvents.CountAsync());
        Assert.Equal(1, (await fixture.Db.WorkOrders.SingleAsync()).SubmissionVersion);
    }

    [Fact]
    public async Task A_stale_supervisor_context_cannot_add_a_conflicting_review()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await ExecutionSubmissionTests.ReadyAsync(fixture, data);
        var submitted = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        fixture.ActAs(data.Supervisor);
        await using var stale = NewContext(fixture);
        await stale.WorkOrders.SingleAsync();
        await stale.WorkOrderSubmissions.SingleAsync();
        var staleService = new WorkOrderReviewService(stale, fixture.Actor,
            new AuditWriter(stale, fixture.Actor, fixture.Clock), fixture.Clock);
        await fixture.Reviews.ApproveAsync(data.WorkOrder.Id, new(submitted.Submission.Id, "Approved first"));
        var historyCount = await fixture.Db.WorkOrderHistoryEvents.CountAsync();
        var auditCount = await fixture.Db.AuditLogs.CountAsync();

        await ModuleFixture.ExpectStatusAsync(409, () => staleService.RejectAsync(
            data.WorkOrder.Id, new(submitted.Submission.Id, "Stale conflicting decision")));

        var review = Assert.Single(await fixture.Db.WorkOrderApprovals.AsNoTracking().ToListAsync());
        Assert.Equal(WorkOrderReviewDecision.APPROVED, review.Decision);
        Assert.Equal("Approved first", review.Remarks);
        Assert.Equal(historyCount, await fixture.Db.WorkOrderHistoryEvents.CountAsync());
        Assert.Equal(auditCount, await fixture.Db.AuditLogs.CountAsync());
    }

    [Theory]
    [InlineData("Submission", false)]
    [InlineData("Checklist", false)]
    [InlineData("Attachment", false)]
    [InlineData("Part", false)]
    [InlineData("Defect", false)]
    [InlineData("Approval", false)]
    [InlineData("History", false)]
    [InlineData("File", false)]
    [InlineData("Submission", true)]
    [InlineData("Checklist", true)]
    [InlineData("Attachment", true)]
    [InlineData("Part", true)]
    [InlineData("Defect", true)]
    [InlineData("Approval", true)]
    [InlineData("History", true)]
    [InlineData("File", true)]
    public async Task Submitted_facts_decisions_and_file_metadata_cannot_be_rewritten_or_deleted(string kind, bool delete)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await ApprovedAsync(fixture);
        fixture.Db.ChangeTracker.Clear();
        var (entity, property) = await ImmutableEntityAsync(fixture.Db, kind);
        var entry = fixture.Db.Entry(entity);
        var before = entry.Property(property).CurrentValue;
        if (delete) entry.State = EntityState.Deleted;
        else entry.Property(property).CurrentValue = "Attempted historical rewrite";

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());

        fixture.Db.ChangeTracker.Clear();
        var (persisted, persistedProperty) = await ImmutableEntityAsync(fixture.Db, kind);
        Assert.Equal(before, fixture.Db.Entry(persisted).Property(persistedProperty).CurrentValue);
    }

    [Theory]
    [InlineData("Execution")]
    [InlineData("Checklist")]
    [InlineData("Part")]
    [InlineData("Defect")]
    [InlineData("Attachment")]
    public async Task Approved_drafts_are_frozen_even_through_direct_entity_updates(string kind)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await ApprovedAsync(fixture);
        fixture.Db.ChangeTracker.Clear();
        (object Entity, string Property) target = kind switch
        {
            "Execution" => (await fixture.Db.WorkOrderExecutions.SingleAsync(), nameof(WorkOrderExecution.OverallComments)),
            "Checklist" => (await fixture.Db.WorkOrderChecklistResults.SingleAsync(), nameof(WorkOrderChecklistResult.Comment)),
            "Part" => (await fixture.Db.SparePartUsages.SingleAsync(), nameof(SparePartUsage.Remarks)),
            "Defect" => (await fixture.Db.WorkOrderDefects.SingleAsync(), nameof(WorkOrderDefect.Description)),
            _ => (await fixture.Db.WorkOrderAttachments.SingleAsync(), nameof(WorkOrderAttachment.Description))
        };
        fixture.Db.Entry(target.Entity).Property(target.Property).CurrentValue = "Cannot change approved execution";

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("Checklist")]
    [InlineData("Attachment")]
    [InlineData("Part")]
    [InlineData("Defect")]
    public async Task Resuming_a_rejected_job_does_not_allow_appending_facts_to_its_old_submission(string kind)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await ExecutionSubmissionTests.ReadyAsync(fixture, data);
        var submitted = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        fixture.ActAs(data.Supervisor);
        await fixture.Reviews.RejectAsync(data.WorkOrder.Id, new(submitted.Submission.Id, "Please correct the draft"));
        fixture.ActAs(data.Technician);
        await fixture.Execution.ResumeAsync(data.WorkOrder.Id);
        var photo = await ExecutionEvidenceTests.UploadPngAsync(fixture, data.WorkOrder.Id);
        var oldSubmissionId = submitted.Submission.Id;
        object lateFact = kind switch
        {
            "Checklist" => new WorkOrderSubmissionChecklistResult { WorkOrderSubmissionId = oldSubmissionId,
                WorkOrderChecklistItemId = data.Item.Id, ConfirmationValue = false, CompletedByUserId = data.Technician.Id },
            "Attachment" => new WorkOrderSubmissionAttachment { WorkOrderSubmissionId = oldSubmissionId,
                FileId = photo.FileId, EvidenceType = EvidenceType.OTHER, UploadedByUserId = data.Technician.Id,
                UploadedAt = fixture.Clock.GetUtcNow().UtcDateTime },
            "Part" => new WorkOrderSubmissionPartUsage { WorkOrderSubmissionId = oldSubmissionId,
                PartName = "Late part", Quantity = 1, CreatedByUserId = data.Technician.Id, CreatedAt = fixture.Clock.GetUtcNow().UtcDateTime },
            _ => new WorkOrderSubmissionDefect { WorkOrderSubmissionId = oldSubmissionId,
                Title = "Late defect", Description = "Must belong to a new submission", CreatedByUserId = data.Technician.Id,
                CreatedAt = fixture.Clock.GetUtcNow().UtcDateTime }
        };
        fixture.Db.Add(lateFact);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());

        fixture.Db.ChangeTracker.Clear();
        var original = await fixture.Submissions.GetAsync(data.WorkOrder.Id, oldSubmissionId);
        Assert.True(Assert.Single(original.ChecklistResults).ConfirmationValue);
        Assert.Empty(original.Attachments);
        Assert.Empty(original.PartUsages);
        Assert.Empty(original.Defects);
    }

    private static ApplicationDbContext NewContext(ModuleFixture fixture) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(fixture.Db.Database.GetDbConnection()).Options);

    private static async Task ApprovedAsync(ModuleFixture fixture)
    {
        var data = await ExecutionTestData.CreateAsync(fixture);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        await fixture.Execution.SaveChecklistResultsAsync(data.WorkOrder.Id,
            new([new ChecklistResultRequest(data.Item.Id, ConfirmationValue: true)]));
        await fixture.Execution.AddPartAsync(data.WorkOrder.Id, new("Bearing", 1));
        await fixture.Execution.AddDefectAsync(data.WorkOrder.Id, new("Small leak", "Follow up after inspection"));
        await ExecutionEvidenceTests.UploadPngAsync(fixture, data.WorkOrder.Id);
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        var submitted = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        fixture.ActAs(data.Supervisor);
        await fixture.Reviews.ApproveAsync(data.WorkOrder.Id, new(submitted.Submission.Id));
    }

    private static async Task<(object Entity, string Property)> ImmutableEntityAsync(ApplicationDbContext db, string kind) => kind switch
    {
        "Submission" => (await db.WorkOrderSubmissions.SingleAsync(), nameof(WorkOrderSubmission.OverallComments)),
        "Checklist" => (await db.WorkOrderSubmissionChecklistResults.SingleAsync(), nameof(WorkOrderSubmissionChecklistResult.Comment)),
        "Attachment" => (await db.WorkOrderSubmissionAttachments.SingleAsync(), nameof(WorkOrderSubmissionAttachment.Description)),
        "Part" => (await db.WorkOrderSubmissionPartUsages.SingleAsync(), nameof(WorkOrderSubmissionPartUsage.Remarks)),
        "Defect" => (await db.WorkOrderSubmissionDefects.SingleAsync(), nameof(WorkOrderSubmissionDefect.Description)),
        "Approval" => (await db.WorkOrderApprovals.SingleAsync(), nameof(WorkOrderApproval.Remarks)),
        "History" => (await db.WorkOrderHistoryEvents.OrderBy(x => x.SequenceNumber).FirstAsync(), nameof(WorkOrderHistoryEvent.Details)),
        _ => (await db.FileRecords.SingleAsync(), nameof(FileRecord.OriginalFilename))
    };
}
