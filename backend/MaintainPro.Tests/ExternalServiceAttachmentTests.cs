using MaintainPro.Application.ExternalServices;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class ExternalServiceAttachmentTests
{
    [Fact]
    public async Task Multiple_service_documents_use_private_storage_and_removal_preserves_history()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        var service = await fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data));
        var report = await UploadAsync(fixture, service.Id, "report.pdf", "application/pdf",
            ExecutionTestData.Pdf(), ExternalServiceDocumentType.SERVICE_REPORT);
        var photo = await UploadAsync(fixture, service.Id, "photo.png", "image/png",
            ExecutionTestData.Png(), ExternalServiceDocumentType.PHOTO);
        var jpeg = new byte[] { 0xff, 0xd8, 0xff, 0xe0, 0, 4, 0, 0, 0xff, 0xd9 };
        await UploadAsync(fixture, service.Id, "condition.jpg", "image/jpeg", jpeg,
            ExternalServiceDocumentType.PHOTO);

        Assert.Equal(3, (await fixture.ExternalServiceAttachments.ListAsync(service.Id)).Count);
        Assert.DoesNotContain("report.pdf", (await fixture.Db.FileRecords.FindAsync(report.FileId))!.StorageKey);
        fixture.ActAs(data.Technician);
        await AssertDownloadAsync(fixture, report.FileId, ExecutionTestData.Pdf());
        await fixture.ExternalServiceAttachments.DeactivateAsync(service.Id, report.Id);
        var retained = Assert.Single((await fixture.ExternalServiceAttachments.ListAsync(service.Id)), x => x.Id == report.Id);
        Assert.False(retained.IsActive);
        Assert.Equal(3, await fixture.Db.FileRecords.CountAsync());
        Assert.Equal(3, await fixture.Db.ExternalServiceAttachments.CountAsync());
        await AssertDownloadAsync(fixture, report.FileId, ExecutionTestData.Pdf());
        Assert.True((await fixture.ExternalServiceAttachments.ListAsync(service.Id)).Single(x => x.Id == photo.Id).IsActive);
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), x => x.Action == "ExternalService.AttachmentDeactivated");
    }

    [Fact]
    public async Task Attachment_upload_rejects_invalid_content_and_does_not_leave_metadata_or_bytes()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        var service = await fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data));
        var bytes = ExecutionTestData.Png();
        await using var stream = new MemoryStream(bytes);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.ExternalServiceAttachments.UploadAsync(service.Id,
            new("bad.pdf", "application/pdf", bytes.Length, ExternalServiceDocumentType.INVOICE), stream));
        await using var invalidExtension = new MemoryStream(bytes);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.ExternalServiceAttachments.UploadAsync(service.Id,
            new("bad.exe", "image/png", bytes.Length, ExternalServiceDocumentType.OTHER), invalidExtension));
        Assert.Empty(await fixture.Db.ExternalServiceAttachments.ToListAsync());
        Assert.Empty(await fixture.Db.FileRecords.ToListAsync());
    }

    [Fact]
    public async Task Unrelated_user_cannot_list_upload_remove_or_download_service_files()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        var service = await fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data));
        var attachment = await UploadAsync(fixture, service.Id, "report.pdf", "application/pdf",
            ExecutionTestData.Pdf(), ExternalServiceDocumentType.SERVICE_REPORT);
        fixture.ActAs(await fixture.SeedUserAsync("TECHNICIAN"));

        await ModuleFixture.ExpectStatusAsync(404, () => fixture.ExternalServiceAttachments.ListAsync(service.Id));
        await ModuleFixture.ExpectStatusAsync(404,
            () => fixture.ExternalServiceAttachments.DeactivateAsync(service.Id, attachment.Id));
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Evidence.DownloadAsync(attachment.FileId));
        var bytes = ExecutionTestData.Png();
        await using var input = new MemoryStream(bytes);
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.ExternalServiceAttachments.UploadAsync(service.Id,
            new("photo.png", "image/png", bytes.Length, ExternalServiceDocumentType.PHOTO), input));
    }

    private static async Task<ExternalServiceAttachmentDto> UploadAsync(ModuleFixture fixture, Guid serviceId,
        string filename, string mime, byte[] bytes, ExternalServiceDocumentType type)
    {
        await using var stream = new MemoryStream(bytes);
        return await fixture.ExternalServiceAttachments.UploadAsync(serviceId,
            new(filename, mime, bytes.Length, type, "Supporting document"), stream);
    }

    private static async Task AssertDownloadAsync(ModuleFixture fixture, Guid fileId, byte[] expected)
    {
        var download = await fixture.Evidence.DownloadAsync(fileId);
        await using var stream = download.Content;
        using var copied = new MemoryStream();
        await stream.CopyToAsync(copied);
        Assert.Equal(expected, copied.ToArray());
    }
}
