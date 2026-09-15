using System.Text.Json;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Breakdowns;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class BreakdownEvidenceTests
{
    [Fact]
    public async Task Reporting_author_can_add_initial_evidence_and_existing_download_route_uses_breakdown_visibility()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        var attachment = await BreakdownTestData.UploadAsync(fixture, data.Breakdown.Id, BreakdownEvidenceType.INITIAL_CONDITION);
        Assert.Equal(data.Administrator.Id, attachment.UploadedByUserId);
        Assert.Equal("image/png", attachment.MimeType);
        Assert.DoesNotContain("StorageKey", JsonSerializer.Serialize(attachment));
        Assert.DoesNotContain(fixture.FileStorageDirectory, JsonSerializer.Serialize(attachment));
        fixture.ActAs(data.Supervisor);
        var download = await fixture.Evidence.DownloadAsync(attachment.FileId);
        await using (download.Content)
        {
            using var output = new MemoryStream();
            await download.Content.CopyToAsync(output);
            Assert.Equal(ExecutionTestData.Png(), output.ToArray());
        }
        await ModuleFixture.ExpectStatusAsync(403, () => BreakdownTestData.UploadAsync(fixture, data.Breakdown.Id, BreakdownEvidenceType.FAILURE));
        fixture.ActAs(data.Technician);
        await ModuleFixture.ExpectStatusAsync(403, () => BreakdownTestData.UploadAsync(fixture, data.Breakdown.Id));
        await fixture.CorrectiveExecution.StartAsync(data.Breakdown.Id);
        Assert.Equal(BreakdownEvidenceType.REPAIR, (await BreakdownTestData.UploadAsync(fixture, data.Breakdown.Id)).EvidenceType);
        fixture.ActAs(await fixture.SeedUserAsync("TECHNICIAN"));
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Evidence.DownloadAsync(attachment.FileId));
        await ModuleFixture.ExpectStatusAsync(404, () => BreakdownTestData.UploadAsync(fixture, data.Breakdown.Id));
    }

    [Fact]
    public async Task Removing_never_submitted_evidence_hides_download_without_deleting_file_history()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture, assigned: false);
        var attachment = await BreakdownTestData.UploadAsync(fixture, data.Breakdown.Id, BreakdownEvidenceType.FAILURE);
        await fixture.BreakdownEvidence.DeleteAsync(data.Breakdown.Id, attachment.Id);
        Assert.Empty((await fixture.CorrectiveExecution.GetAsync(data.Breakdown.Id)).Attachments);
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Evidence.DownloadAsync(attachment.FileId));
        Assert.True((await fixture.Db.BreakdownAttachments.SingleAsync()).IsDeleted);
        Assert.Single(await fixture.Db.FileRecords.ToListAsync());
    }

    [Fact]
    public async Task Removed_draft_evidence_remains_downloadable_through_the_rejected_immutable_submission()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);
        await fixture.CorrectiveExecution.StartAsync(data.Breakdown.Id);
        var attachment = await BreakdownTestData.UploadAsync(fixture, data.Breakdown.Id);
        await fixture.CorrectiveExecution.SaveAsync(data.Breakdown.Id, new("Cause", "Repair"));
        await fixture.CorrectiveExecution.CompleteAsync(data.Breakdown.Id);
        var submission = await fixture.CorrectiveSubmissions.SubmitAsync(data.Breakdown.Id);
        fixture.ActAs(data.Supervisor);
        await fixture.CorrectiveReviews.RejectAsync(data.Breakdown.Id, new(submission.Submission.Id, "Photograph final condition"));
        fixture.ActAs(data.Technician);
        await fixture.CorrectiveExecution.ResumeAsync(data.Breakdown.Id);
        await fixture.BreakdownEvidence.DeleteAsync(data.Breakdown.Id, attachment.Id);
        var original = await fixture.CorrectiveSubmissions.GetAsync(data.Breakdown.Id, submission.Submission.Id);
        Assert.Equal(attachment.FileId, Assert.Single(original.Attachments).FileId);
        var download = await fixture.Evidence.DownloadAsync(attachment.FileId);
        await using (download.Content) Assert.True(download.Content.CanRead);
        Assert.Empty((await fixture.CorrectiveExecution.GetAsync(data.Breakdown.Id)).Attachments);
    }

    [Fact]
    public async Task Evidence_change_after_completion_requires_completion_again_and_submitted_evidence_is_locked()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);
        await fixture.CorrectiveExecution.StartAsync(data.Breakdown.Id);
        await fixture.CorrectiveExecution.SaveAsync(data.Breakdown.Id, new("Cause", "Repair"));
        await fixture.CorrectiveExecution.CompleteAsync(data.Breakdown.Id);
        var attachment = await BreakdownTestData.UploadAsync(fixture, data.Breakdown.Id, BreakdownEvidenceType.FINAL_CONDITION);
        Assert.False((await fixture.CorrectiveExecution.GetAsync(data.Breakdown.Id)).IsComplete);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.CorrectiveSubmissions.SubmitAsync(data.Breakdown.Id));
        await fixture.CorrectiveExecution.CompleteAsync(data.Breakdown.Id);
        await fixture.CorrectiveSubmissions.SubmitAsync(data.Breakdown.Id);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.BreakdownEvidence.DeleteAsync(data.Breakdown.Id, attachment.Id));
        await ModuleFixture.ExpectStatusAsync(409, () => BreakdownTestData.UploadAsync(fixture, data.Breakdown.Id));
    }

    [Fact]
    public async Task Failed_evidence_audit_removes_new_bytes_and_rolls_back_attachment_metadata()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture, assigned: false);
        var service = new BreakdownEvidenceService(fixture.Db, fixture.Actor, new FailingAudit(), fixture.Clock, fixture.FileStorage);
        var bytes = ExecutionTestData.Png();
        await using var stream = new MemoryStream(bytes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(data.Breakdown.Id,
            new("condition.png", "image/png", bytes.Length, BreakdownEvidenceType.INITIAL_CONDITION), stream));
        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Db.BreakdownAttachments.ToListAsync());
        Assert.Empty(await fixture.Db.FileRecords.ToListAsync());
        if (Directory.Exists(fixture.FileStorageDirectory)) Assert.Empty(Directory.EnumerateFiles(fixture.FileStorageDirectory));
    }

    private sealed class FailingAudit : IAuditWriter
    {
        public void Record(string action, string entityType, Guid? entityId, object? oldValues = null,
            object? newValues = null) => throw new InvalidOperationException("Simulated evidence audit failure.");
    }
}
