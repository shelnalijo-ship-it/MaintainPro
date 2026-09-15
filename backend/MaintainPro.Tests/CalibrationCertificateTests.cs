using MaintainPro.Application.Calibrations;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class CalibrationCertificateTests
{
    [Fact]
    public async Task Manager_creates_normalized_certificates_and_history_is_append_only()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);

        var first = await fixture.Calibrations.CreateAsync(new(data.Machine.Id, " cert-001 ", " lab one ",
            data.Today.AddDays(-100), data.Today.AddDays(20), CalibrationResult.PASS, "Initial certificate"));
        var second = await fixture.Calibrations.CreateAsync(new(data.Machine.Id, "CERT-002", "LAB ONE",
            data.Today.AddDays(-5), data.Today.AddDays(365), CalibrationResult.CONDITIONAL));

        Assert.Equal("CERT-001", first.CertificateNumber);
        Assert.Equal("LAB ONE", first.CalibrationProvider);
        Assert.Equal(2, await fixture.Db.CalibrationCertificates.CountAsync());
        var history = await fixture.Calibrations.MachineHistoryAsync(data.Machine.Id, new());
        Assert.Equal(new[] { second.Id, first.Id }, history.Items.Select(x => x.Id));
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), x => x.Action == "CalibrationCertificate.Created");
    }

    [Fact]
    public async Task Certificate_uniqueness_is_scoped_to_normalized_provider()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        await CalibrationTestData.AddCertificateAsync(fixture, data, "CERT-X");

        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Calibrations.CreateAsync(new(data.Machine.Id,
            " cert-x ", " accredited lab ", data.Today.AddDays(-2), data.Today.AddDays(200), CalibrationResult.PASS)));
        var otherProvider = await fixture.Calibrations.CreateAsync(new(data.Machine.Id, "CERT-X", "Second Lab",
            data.Today.AddDays(-2), data.Today.AddDays(200), CalibrationResult.PASS));

        Assert.Equal("SECOND LAB", otherProvider.CalibrationProvider);
    }

    [Fact]
    public async Task Creation_validates_machine_dates_result_and_non_required_override()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Calibrations.CreateAsync(new(Guid.NewGuid(),
            "A", "LAB", data.Today, data.Today.AddDays(1), CalibrationResult.PASS)));
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Calibrations.CreateAsync(new(data.Machine.Id,
            "B", "LAB", data.Today, data.Today, CalibrationResult.PASS)));
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Calibrations.CreateAsync(new(data.Machine.Id,
            "C", "LAB", data.Today, data.Today.AddDays(1), (CalibrationResult)999)));

        var notRequired = await fixture.SeedMachineAsync(configure: x => x.CalibrationRequired = false);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Calibrations.CreateAsync(new(notRequired.Id,
            "D", "LAB", data.Today, data.Today.AddDays(1), CalibrationResult.PASS)));
        var recorded = await fixture.Calibrations.CreateAsync(new(notRequired.Id, "D", "LAB", data.Today,
            data.Today.AddDays(1), CalibrationResult.PASS, RecordForNonRequiredMachine: true));
        Assert.Equal(CalibrationValidityStatus.NOT_REQUIRED, recorded.ValidityStatus);
    }

    [Fact]
    public async Task Only_manager_or_admin_can_administer_certificates()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);
        await ModuleFixture.ExpectStatusAsync(403, () => CalibrationTestData.AddCertificateAsync(fixture, data));
        fixture.ActAs(data.Supervisor);
        await ModuleFixture.ExpectStatusAsync(403, () => CalibrationTestData.AddCertificateAsync(fixture, data));
        fixture.ActAs(data.Administrator);
        Assert.NotNull(await CalibrationTestData.AddCertificateAsync(fixture, data));
    }

    [Fact]
    public async Task Certificate_documents_reuse_private_storage_and_machine_scoped_download()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        var bytes = ExecutionTestData.Pdf();
        await using var upload = new MemoryStream(bytes);
        var certificate = await fixture.Calibrations.CreateAsync(new(data.Machine.Id, "FILE-1", "LAB",
            data.Today.AddDays(-5), data.Today.AddDays(100), CalibrationResult.PASS),
            new("certificate.pdf", "application/pdf", bytes.Length), upload);

        var fileId = Assert.IsType<Guid>(certificate.CertificateFileId);
        var metadata = await fixture.Db.FileRecords.SingleAsync(x => x.Id == fileId);
        Assert.DoesNotContain("certificate.pdf", metadata.StorageKey);
        fixture.ActAs(data.Supervisor);
        await using (var download = (await fixture.Evidence.DownloadAsync(fileId)).Content)
        {
            using var copied = new MemoryStream();
            await download.CopyToAsync(copied);
            Assert.Equal(bytes, copied.ToArray());
        }
        fixture.ActAs(await fixture.SeedUserAsync("TECHNICIAN"));
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Evidence.DownloadAsync(fileId));
    }

    [Fact]
    public async Task Certificate_history_cannot_be_updated_or_deleted()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        var created = await CalibrationTestData.AddCertificateAsync(fixture, data);
        fixture.Db.ChangeTracker.Clear();
        var certificate = await fixture.Db.CalibrationCertificates.SingleAsync(x => x.Id == created.Id);
        certificate.ExpiryDate = certificate.ExpiryDate.AddDays(30);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());

        fixture.Db.Entry(certificate).State = EntityState.Unchanged;
        fixture.Db.CalibrationCertificates.Remove(certificate);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Reads_apply_machine_scope_before_pagination_and_hide_foreign_history()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        var own = await CalibrationTestData.AddCertificateAsync(fixture, data);
        var otherTechnician = await fixture.SeedUserAsync("TECHNICIAN");
        var otherSupervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var hiddenMachine = await fixture.SeedMachineAsync(otherTechnician.Id, otherSupervisor.Id,
            x => x.CalibrationRequired = true);
        await fixture.Calibrations.CreateAsync(new(hiddenMachine.Id, "HIDDEN", "LAB", data.Today.AddDays(-1),
            data.Today.AddDays(100), CalibrationResult.PASS));
        var hiddenCertificateId = await fixture.Db.CalibrationCertificates.Where(x => x.MachineId == hiddenMachine.Id)
            .Select(x => x.Id).SingleAsync();

        fixture.ActAs(data.Technician);
        var page = await fixture.Calibrations.ListAsync(new(Page: 1, PageSize: 1));
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(own.Id, Assert.Single(page.Items).Id);
        await ModuleFixture.ExpectStatusAsync(404,
            () => fixture.Calibrations.MachineHistoryAsync(hiddenMachine.Id, new()));
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Calibrations.GetAsync(hiddenCertificateId));
    }

    [Fact]
    public async Task Machine_read_models_include_current_calibration_without_per_row_queries()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        await CalibrationTestData.AddCertificateAsync(fixture, data, expiresInDays: 30);

        var machine = await fixture.Machines.GetAsync(data.Machine.Id);
        var listed = Assert.Single((await fixture.Machines.ListAsync(new(Search: data.Machine.MachineCode))).Items);

        Assert.Equal("CERT-001", machine.CurrentCertificateNumber);
        Assert.Equal(data.Today.AddDays(30), machine.CalibrationExpiryDate);
        Assert.Equal(30, machine.DaysUntilCalibrationExpiry);
        Assert.Equal(CalibrationValidityStatus.EXPIRING_SOON, listed.CurrentCalibrationStatus);
    }
}
