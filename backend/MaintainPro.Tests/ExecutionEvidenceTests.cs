using System.Text;
using System.Text.Json;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Execution;
using MaintainPro.Application.Planning;
using MaintainPro.Application.Reviews;
using MaintainPro.Domain.Enums;
using MaintainPro.Infrastructure.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MaintainPro.Tests;

public sealed class ExecutionEvidenceTests
{
    [Theory]
    [InlineData("condition.png", "image/png")]
    [InlineData("condition.jpg", "image/jpeg")]
    [InlineData("condition.jpeg", "image/jpeg")]
    [InlineData("inspection.pdf", "application/pdf")]
    public async Task Local_storage_accepts_supported_formats_and_uses_generated_private_keys(string name, string mime)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var bytes = mime switch
        {
            "image/png" => ExecutionTestData.Png(),
            "application/pdf" => ExecutionTestData.Pdf(),
            _ => new byte[] { 0xff, 0xd8, 0xff, 0xe0, 0, 4, 0, 0, 0xff, 0xd9 }
        };
        await using var input = new MemoryStream(bytes);
        var stored = await fixture.FileStorage.StoreAsync(input, name, mime, bytes.Length);
        Assert.True(Guid.TryParseExact(stored.StorageKey, "N", out _));
        Assert.Equal(name, stored.OriginalFilename);
        Assert.Equal(bytes.Length, stored.FileSize);
        Assert.DoesNotContain(name, stored.StorageKey);
        Assert.True(File.Exists(Path.Combine(fixture.FileStorageDirectory, stored.StorageKey)));
        await using var output = await fixture.FileStorage.OpenReadAsync(stored.StorageKey);
        using var copied = new MemoryStream();
        await output.CopyToAsync(copied);
        Assert.Equal(bytes, copied.ToArray());
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("..\\outside.png")]
    [InlineData("C:\\outside.png")]
    [InlineData("/outside.png")]
    [InlineData("bad\nname.png")]
    public async Task Original_filenames_cannot_control_storage_paths(string filename)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var bytes = ExecutionTestData.Png();
        await using var input = new MemoryStream(bytes);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.FileStorage.StoreAsync(input, filename, "image/png", bytes.Length));
        Assert.False(Directory.Exists(fixture.FileStorageDirectory));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("/etc/passwd")]
    [InlineData("0123456789abcdef0123456789abcdef/extra")]
    public async Task Read_and_delete_reject_non_generated_storage_keys(string key)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.FileStorage.OpenReadAsync(key));
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.FileStorage.DeleteAsync(key));
    }

    [Theory]
    [InlineData("file.exe", "application/octet-stream", false)]
    [InlineData("file.png", "application/pdf", false)]
    [InlineData("file.png", "image/png", true)]
    [InlineData("file.pdf", "application/pdf", true)]
    [InlineData("file.jpg", "image/jpeg", true)]
    public async Task Extension_mime_and_actual_file_structure_must_agree(string filename, string mime, bool fakeBytes)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var bytes = fakeBytes ? Encoding.UTF8.GetBytes("This is not the claimed binary file format.") : ExecutionTestData.Png();
        await using var input = new MemoryStream(bytes);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.FileStorage.StoreAsync(input, filename, mime, bytes.Length));
        Assert.False(Directory.Exists(fixture.FileStorageDirectory));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task File_size_limit_checks_both_declared_length_and_actual_stream_bytes(bool oversizedDeclaration)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var storage = new LocalFileStorageService(Options.Create(new FileStorageOptions
        {
            RootDirectory = fixture.FileStorageDirectory, MaxFileSizeBytes = 32
        }));
        var bytes = ExecutionTestData.Png();
        await using var input = new MemoryStream(bytes);
        await ModuleFixture.ExpectStatusAsync(413, () => storage.StoreAsync(input, "file.png", "image/png",
            oversizedDeclaration ? bytes.Length : 1));
        Assert.False(Directory.Exists(fixture.FileStorageDirectory));
    }

    [Fact]
    public async Task Length_mismatch_is_rejected_without_persisting_a_file()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var bytes = ExecutionTestData.Png();
        await using var input = new MemoryStream(bytes);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.FileStorage.StoreAsync(input, "file.png", "image/png", bytes.Length + 1));
        Assert.False(Directory.Exists(fixture.FileStorageDirectory));
    }

    [Fact]
    public async Task Evidence_upload_download_and_scoping_preserve_bytes_without_exposing_storage_paths()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        var attachment = await UploadPngAsync(fixture, data.WorkOrder.Id, data.Item.Id);
        var json = JsonSerializer.Serialize(attachment);
        Assert.DoesNotContain("StorageKey", json);
        Assert.DoesNotContain(fixture.FileStorageDirectory, json);
        await AssertDownloadAsync(fixture, attachment.FileId);
        fixture.ActAs(data.Supervisor);
        await AssertDownloadAsync(fixture, attachment.FileId);
        fixture.ActAs(data.Administrator);
        await AssertDownloadAsync(fixture, attachment.FileId);
        fixture.ActAs(await fixture.SeedUserAsync("TECHNICIAN"));
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Evidence.DownloadAsync(attachment.FileId));
        await ModuleFixture.ExpectStatusAsync(404, () => UploadPngAsync(fixture, data.WorkOrder.Id, data.Item.Id));
        Assert.Equal(1, await fixture.Db.FileRecords.CountAsync());
        await fixture.AssertAuditHasNoSecretsAsync(Convert.ToBase64String(ExecutionTestData.Png()), fixture.Password);
    }

    [Fact]
    public async Task Evidence_rejects_foreign_checklist_links_and_invalid_categories_without_files()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        await ModuleFixture.ExpectStatusAsync(400, () => UploadPngAsync(fixture, data.WorkOrder.Id, Guid.NewGuid()));
        var bytes = ExecutionTestData.Png();
        await using var stream = new MemoryStream(bytes);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Evidence.UploadAsync(data.WorkOrder.Id,
            new("condition.png", "image/png", bytes.Length, (EvidenceType)999), stream));
        Assert.Empty(await fixture.Db.FileRecords.ToListAsync());
    }

    [Fact]
    public async Task Evidence_metadata_and_bytes_are_rolled_back_when_the_audit_write_fails()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        var failing = new WorkOrderEvidenceService(fixture.Db, fixture.Actor, new FailingAudit(), fixture.Clock, fixture.FileStorage);
        var bytes = ExecutionTestData.Png();
        await using var input = new MemoryStream(bytes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.UploadAsync(data.WorkOrder.Id,
            new("condition.png", "image/png", bytes.Length, EvidenceType.OTHER), input));
        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Db.FileRecords.ToListAsync());
        Assert.Empty(await fixture.Db.WorkOrderAttachments.ToListAsync());
        Assert.Empty(Directory.GetFiles(fixture.FileStorageDirectory));
    }

    [Fact]
    public async Task Required_checklist_photos_must_be_linked_images_and_deleted_drafts_do_not_count()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture,
            new ChecklistWriteRequest("Photo evidence", [new ChecklistItemWriteRequest(1, "Condition photo", ChecklistResponseType.PHOTO,
                PhotoRequired: true)]), photoRequired: true, minimumPhotoCount: 2);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        var pdf = ExecutionTestData.Pdf();
        await using var pdfStream = new MemoryStream(pdf);
        await fixture.Evidence.UploadAsync(data.WorkOrder.Id,
            new("support.pdf", "application/pdf", pdf.Length, EvidenceType.OTHER, data.Item.Id), pdfStream);
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Submissions.SubmitAsync(data.WorkOrder.Id));
        var first = await UploadPngAsync(fixture, data.WorkOrder.Id, data.Item.Id);
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Submissions.SubmitAsync(data.WorkOrder.Id));
        await UploadPngAsync(fixture, data.WorkOrder.Id, data.Item.Id);
        await fixture.Evidence.DeleteAsync(data.WorkOrder.Id, first.Id);
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Submissions.SubmitAsync(data.WorkOrder.Id));
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Evidence.DownloadAsync(first.FileId));
        await UploadPngAsync(fixture, data.WorkOrder.Id, data.Item.Id);
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        var submission = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        Assert.Equal(2, submission.Attachments.Count(x => x.MimeType == "image/png"));
        Assert.DoesNotContain(submission.Attachments, x => x.FileId == first.FileId);
    }

    [Fact]
    public async Task Previously_submitted_evidence_remains_downloadable_after_rejection_and_draft_removal()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        var attachment = await UploadPngAsync(fixture, data.WorkOrder.Id, data.Item.Id);
        await fixture.Execution.SaveChecklistResultsAsync(data.WorkOrder.Id,
            new([new ChecklistResultRequest(data.Item.Id, ConfirmationValue: true)]));
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        var first = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        fixture.ActAs(data.Supervisor);
        await fixture.Reviews.RejectAsync(data.WorkOrder.Id, new(first.Submission.Id, "Replace the supporting image"));
        fixture.ActAs(data.Technician);
        await fixture.Execution.ResumeAsync(data.WorkOrder.Id);
        await fixture.Evidence.DeleteAsync(data.WorkOrder.Id, attachment.Id);
        Assert.Empty((await fixture.Execution.GetAsync(data.WorkOrder.Id)).Attachments);
        await AssertDownloadAsync(fixture, attachment.FileId);
        var history = await fixture.Submissions.GetAsync(data.WorkOrder.Id, first.Submission.Id);
        Assert.Equal(attachment.FileId, Assert.Single(history.Attachments).FileId);
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        var second = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        Assert.Empty(second.Attachments);
        Assert.Single((await fixture.Submissions.GetAsync(data.WorkOrder.Id, first.Submission.Id)).Attachments);
    }

    internal static async Task<AttachmentDto> UploadPngAsync(ModuleFixture fixture, Guid workOrderId, Guid? itemId = null)
    {
        var bytes = ExecutionTestData.Png();
        await using var stream = new MemoryStream(bytes);
        return await fixture.Evidence.UploadAsync(workOrderId,
            new("condition.png", "image/png", bytes.Length, EvidenceType.AFTER_MAINTENANCE, itemId, "Inspection evidence"), stream);
    }

    private static async Task AssertDownloadAsync(ModuleFixture fixture, Guid fileId)
    {
        var file = await fixture.Evidence.DownloadAsync(fileId);
        await using var stream = file.Content;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        Assert.Equal(ExecutionTestData.Png(), buffer.ToArray());
        Assert.Equal("image/png", file.MimeType);
    }

    private sealed class FailingAudit : IAuditWriter
    {
        public void Record(string action, string entityType, Guid? entityId, object? oldValues = null,
            object? newValues = null) => throw new InvalidOperationException("Simulated evidence audit failure.");
    }
}
