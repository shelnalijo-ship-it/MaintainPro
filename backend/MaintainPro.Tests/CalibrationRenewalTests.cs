using MaintainPro.Application.Calibrations;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class CalibrationRenewalTests
{
    [Fact]
    public async Task Renewal_start_is_audited_and_duplicate_active_renewal_is_prevented()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        var previous = await CalibrationTestData.AddCertificateAsync(fixture, data, expiresInDays: 10);

        var renewal = await fixture.CalibrationRenewals.StartAsync(data.Machine.Id, new("Arrange accredited provider"));

        Assert.Equal(CalibrationRenewalStatus.IN_PROGRESS, renewal.Status);
        Assert.Equal(previous.Id, renewal.PreviousCertificateId);
        await ModuleFixture.ExpectStatusAsync(409,
            () => fixture.CalibrationRenewals.StartAsync(data.Machine.Id, new()));
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), x => x.Action == "CalibrationRenewal.Started");
    }

    [Fact]
    public async Task Completion_links_a_new_certificate_and_preserves_the_previous_certificate()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        var previous = await CalibrationTestData.AddCertificateAsync(fixture, data, expiresInDays: 5);
        var renewal = await fixture.CalibrationRenewals.StartAsync(data.Machine.Id, new());
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        var replacement = await CalibrationTestData.AddCertificateAsync(fixture, data, "CERT-NEW",
            calibratedDaysAgo: 0, expiresInDays: 365);

        var completed = await fixture.CalibrationRenewals.CompleteAsync(data.Machine.Id,
            new(replacement.Id, "Certificate received"));

        Assert.Equal(CalibrationRenewalStatus.COMPLETED, completed.Status);
        Assert.Equal(replacement.Id, completed.CompletedCertificateId);
        Assert.Equal(2, await fixture.Db.CalibrationCertificates.CountAsync());
        Assert.NotNull(await fixture.Db.CalibrationCertificates.FindAsync(previous.Id));
        Assert.Equal(replacement.Id, (await fixture.Calibrations.MachineStatusAsync(data.Machine.Id)).CurrentCertificateId);
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), x => x.Action == "CalibrationRenewal.Completed");
        Assert.Equal(new[] { renewal.Id }, (await fixture.CalibrationRenewals.ListAsync(data.Machine.Id)).Select(x => x.Id));
    }

    [Fact]
    public async Task Completion_rejects_previous_or_foreign_certificates()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        var previous = await CalibrationTestData.AddCertificateAsync(fixture, data);
        await fixture.CalibrationRenewals.StartAsync(data.Machine.Id, new());

        await ModuleFixture.ExpectStatusAsync(409, () => fixture.CalibrationRenewals.CompleteAsync(
            data.Machine.Id, new(previous.Id)));
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.CalibrationRenewals.CompleteAsync(
            data.Machine.Id, new(Guid.NewGuid())));
    }

    [Fact]
    public async Task Cancellation_is_retained_and_allows_a_later_renewal()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        await CalibrationTestData.AddCertificateAsync(fixture, data);
        var first = await fixture.CalibrationRenewals.StartAsync(data.Machine.Id, new());

        var cancelled = await fixture.CalibrationRenewals.CancelAsync(data.Machine.Id, new("Provider unavailable"));
        var second = await fixture.CalibrationRenewals.StartAsync(data.Machine.Id, new());

        Assert.Equal(first.Id, cancelled.Id);
        Assert.Equal(CalibrationRenewalStatus.CANCELLED, cancelled.Status);
        Assert.Equal(CalibrationRenewalStatus.IN_PROGRESS, second.Status);
        Assert.Equal(2, (await fixture.CalibrationRenewals.ListAsync(data.Machine.Id)).Count);
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), x => x.Action == "CalibrationRenewal.Cancelled");
    }

    [Fact]
    public async Task Expired_machine_remains_expired_while_renewal_is_in_progress()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        await CalibrationTestData.AddCertificateAsync(fixture, data, expiresInDays: -1);
        await fixture.CalibrationRenewals.StartAsync(data.Machine.Id, new());

        var state = await fixture.Calibrations.MachineStatusAsync(data.Machine.Id);

        Assert.Equal(CalibrationValidityStatus.EXPIRED, state.ValidityStatus);
        Assert.Equal(CalibrationRenewalStatus.IN_PROGRESS, state.RenewalStatus);
    }

    [Fact]
    public async Task Technician_cannot_start_complete_or_cancel_renewals()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await CalibrationTestData.CreateAsync(fixture);
        var certificate = await CalibrationTestData.AddCertificateAsync(fixture, data);
        fixture.ActAs(data.Technician);

        await ModuleFixture.ExpectStatusAsync(403, () => fixture.CalibrationRenewals.StartAsync(data.Machine.Id, new()));
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.CalibrationRenewals.CompleteAsync(data.Machine.Id, new(certificate.Id)));
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.CalibrationRenewals.CancelAsync(data.Machine.Id, new()));
    }
}
