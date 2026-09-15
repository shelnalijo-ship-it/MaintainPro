using MaintainPro.Application.ExternalServices;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class MachineDocumentTests
{
    [Fact]
    public async Task Supervisor_uploads_general_documents_with_derived_expiry_and_secure_download()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        fixture.ActAs(data.Supervisor);
        var manual = await UploadAsync(fixture, data.Machine.Id, "manual.pdf", MachineDocumentType.MANUAL,
            data.Today.AddYears(1));
        var datasheet = await UploadAsync(fixture, data.Machine.Id, "datasheet.png", MachineDocumentType.DATASHEET,
            data.Today.AddDays(60), ExecutionTestData.Png(), "image/png");

        Assert.Equal(MachineDocumentExpiryStatus.VALID, manual.ExpiryStatus);
        Assert.Equal(365, manual.DaysUntilExpiry);
        Assert.Equal(MachineDocumentExpiryStatus.EXPIRING_SOON, datasheet.ExpiryStatus);
        Assert.True(datasheet.IsExpiringSoon);
        fixture.ActAs(data.Technician);
        Assert.Equal(2, (await fixture.MachineDocuments.ListAsync(data.Machine.Id, new())).TotalCount);
        await AssertDownloadAsync(fixture, manual.FileId);
    }

    [Fact]
    public async Task Expiry_day_is_expired_and_no_expiry_is_explicit()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        var expired = await UploadAsync(fixture, data.Machine.Id, "warranty.pdf", MachineDocumentType.WARRANTY,
            data.Today);
        var permanent = await UploadAsync(fixture, data.Machine.Id, "drawing.pdf", MachineDocumentType.DRAWING, null);

        Assert.True(expired.IsExpired);
        Assert.Equal(0, expired.DaysUntilExpiry);
        Assert.Equal(MachineDocumentExpiryStatus.NO_EXPIRY, permanent.ExpiryStatus);
        Assert.Null(permanent.DaysUntilExpiry);
        Assert.Single((await fixture.MachineDocuments.ListAsync(data.Machine.Id,
            new(ExpiryStatus: MachineDocumentExpiryStatus.EXPIRED))).Items);
    }

    [Fact]
    public async Task Metadata_update_and_status_change_are_audited_without_replacing_file_provenance()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        var document = await UploadAsync(fixture, data.Machine.Id, "manual.pdf", MachineDocumentType.MANUAL,
            data.Today.AddDays(90));

        var updated = await fixture.MachineDocuments.UpdateAsync(data.Machine.Id, document.Id,
            new(MachineDocumentType.SOP, "Updated controlled title", "Corrected metadata",
                data.Today.AddDays(-10), data.Today.AddDays(120)));
        var inactive = await fixture.MachineDocuments.SetStatusAsync(data.Machine.Id, document.Id, new(false));

        Assert.Equal(document.FileId, updated.FileId);
        Assert.Equal(document.UploadedByUserId, updated.UploadedByUserId);
        Assert.Equal(MachineDocumentType.SOP, updated.DocumentType);
        Assert.False(inactive.IsActive);
        Assert.NotNull(await fixture.Db.MachineDocuments.FindAsync(document.Id));
        Assert.NotNull(await fixture.Db.FileRecords.FindAsync(document.FileId));
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), x => x.Action == "MachineDocument.Updated");
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), x => x.Action == "MachineDocument.StatusChanged");
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.MachineDocuments.UpdateAsync(data.Machine.Id,
            document.Id, new(MachineDocumentType.SOP, "Blocked while inactive")));
        Assert.True((await fixture.MachineDocuments.SetStatusAsync(data.Machine.Id, document.Id, new(true))).IsActive);
    }

    [Fact]
    public async Task Technician_and_unrelated_users_cannot_administer_machine_documents()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);
        var bytes = ExecutionTestData.Pdf();
        await using var stream = new MemoryStream(bytes);
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.MachineDocuments.CreateAsync(data.Machine.Id,
            new("manual.pdf", "application/pdf", bytes.Length, MachineDocumentType.MANUAL, "Manual"), stream));

        fixture.ActAs(data.Manager);
        var document = await UploadAsync(fixture, data.Machine.Id, "manual.pdf", MachineDocumentType.MANUAL, null);
        fixture.ActAs(await fixture.SeedUserAsync("TECHNICIAN"));
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.MachineDocuments.GetAsync(data.Machine.Id, document.Id));
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Evidence.DownloadAsync(document.FileId));
    }

    [Fact]
    public async Task Document_validation_and_filters_apply_before_pagination()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        await UploadAsync(fixture, data.Machine.Id, "manual.pdf", MachineDocumentType.MANUAL, null);
        await UploadAsync(fixture, data.Machine.Id, "warranty.pdf", MachineDocumentType.WARRANTY, data.Today.AddDays(10));
        var filtered = await fixture.MachineDocuments.ListAsync(data.Machine.Id,
            new(Search: "warranty", Page: 1, PageSize: 1, DocumentType: MachineDocumentType.WARRANTY));
        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal("warranty.pdf", Assert.Single(filtered.Items).OriginalFilename);

        var bytes = ExecutionTestData.Pdf();
        await using var invalid = new MemoryStream(bytes);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.MachineDocuments.CreateAsync(data.Machine.Id,
            new("invalid.pdf", "application/pdf", bytes.Length, MachineDocumentType.WARRANTY, "Invalid",
                DocumentDate: data.Today, ExpiryDate: data.Today.AddDays(-1)), invalid));
    }

    [Fact]
    public async Task Physical_document_and_file_identity_changes_are_blocked()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        var created = await UploadAsync(fixture, data.Machine.Id, "manual.pdf", MachineDocumentType.MANUAL, null);
        fixture.Db.ChangeTracker.Clear();
        var document = await fixture.Db.MachineDocuments.SingleAsync(x => x.Id == created.Id);
        fixture.Db.MachineDocuments.Remove(document);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.Entry(document).State = EntityState.Unchanged;
        document.FileId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    private static async Task<MachineDocumentDto> UploadAsync(ModuleFixture fixture, Guid machineId,
        string filename, MachineDocumentType type, DateOnly? expiry, byte[]? bytes = null,
        string mime = "application/pdf")
    {
        bytes ??= ExecutionTestData.Pdf();
        await using var stream = new MemoryStream(bytes);
        return await fixture.MachineDocuments.CreateAsync(machineId,
            new(filename, mime, bytes.Length, type, filename, DocumentDate: expiry?.AddDays(-30),
                ExpiryDate: expiry), stream);
    }

    private static async Task AssertDownloadAsync(ModuleFixture fixture, Guid fileId)
    {
        var download = await fixture.Evidence.DownloadAsync(fileId);
        await using var content = download.Content;
        using var copied = new MemoryStream();
        await content.CopyToAsync(copied);
        Assert.NotEmpty(copied.ToArray());
    }
}
