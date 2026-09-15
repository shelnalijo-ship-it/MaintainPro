using MaintainPro.Application.Calibrations;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Tests;

public sealed class CalibrationTimingTests
{
    private static readonly DateOnly Today = new(2026, 9, 15);
    private readonly CalibrationTimingService timing = new();

    [Theory]
    [InlineData(61, CalibrationValidityStatus.VALID)]
    [InlineData(60, CalibrationValidityStatus.EXPIRING_SOON)]
    [InlineData(30, CalibrationValidityStatus.EXPIRING_SOON)]
    [InlineData(7, CalibrationValidityStatus.EXPIRING_SOON)]
    [InlineData(1, CalibrationValidityStatus.EXPIRING_SOON)]
    [InlineData(0, CalibrationValidityStatus.EXPIRED)]
    [InlineData(-1, CalibrationValidityStatus.EXPIRED)]
    public void Validity_boundaries_are_explicit(int days, CalibrationValidityStatus expected)
    {
        var machine = Machine(required: true);
        var certificate = Certificate("A", Today.AddDays(-30), Today.AddDays(days));

        var state = timing.Evaluate(machine, certificate, false, Today);

        Assert.Equal(expected, state.ValidityStatus);
        Assert.Equal(days, state.DaysRemaining);
    }

    [Fact]
    public void Not_required_and_missing_certificate_states_are_distinct()
    {
        Assert.Equal(CalibrationValidityStatus.NOT_REQUIRED,
            timing.Evaluate(Machine(false), null, false, Today).ValidityStatus);
        Assert.Equal(CalibrationValidityStatus.EXPIRED,
            timing.Evaluate(Machine(true), null, false, Today).ValidityStatus);
    }

    [Fact]
    public void Current_selection_uses_latest_applicable_certificate_and_retains_overlap()
    {
        var old = Certificate("OLD", Today.AddDays(-100), Today.AddDays(20));
        var newer = Certificate("NEW", Today.AddDays(-5), Today.AddDays(360));

        var selected = timing.Current(new[] { old, newer }, Today);

        Assert.Same(newer, selected);
    }

    [Fact]
    public void Failed_and_future_dated_certificates_do_not_replace_current_certificate()
    {
        var current = Certificate("CURRENT", Today.AddDays(-40), Today.AddDays(40));
        var failed = Certificate("FAILED", Today.AddDays(-1), Today.AddDays(365), CalibrationResult.FAIL);
        var future = Certificate("FUTURE", Today.AddDays(1), Today.AddDays(366));

        Assert.Same(current, timing.Current(new[] { current, failed, future }, Today));
    }

    [Fact]
    public void Renewal_is_exposed_separately_and_does_not_hide_expiry()
    {
        var state = timing.Evaluate(Machine(true), Certificate("A", Today.AddDays(-100), Today.AddDays(-1)), true, Today);

        Assert.Equal(CalibrationValidityStatus.EXPIRED, state.ValidityStatus);
        Assert.Equal(CalibrationRenewalStatus.IN_PROGRESS, state.RenewalStatus);
    }

    private static Machine Machine(bool required) => new()
    {
        MachineCode = "MC-1", Name = "Machine", CalibrationRequired = required
    };

    private static CalibrationCertificate Certificate(string number, DateOnly calibrated, DateOnly expiry,
        CalibrationResult result = CalibrationResult.PASS) => new()
    {
        MachineId = Guid.NewGuid(), CertificateNumber = number, CalibrationProvider = "LAB",
        CalibrationDate = calibrated, ExpiryDate = expiry, Result = result,
        CreatedByUserId = Guid.NewGuid(), CreatedAt = calibrated.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
    };
}
