using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Calibrations;

public sealed class CalibrationTimingService
{
    public CalibrationCertificate? Current(IEnumerable<CalibrationCertificate> certificates, DateOnly today) =>
        certificates.Where(x => x.CalibrationDate <= today && x.Result != CalibrationResult.FAIL)
            .OrderByDescending(x => x.CalibrationDate).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .FirstOrDefault();

    public MachineCalibrationStatusDto Evaluate(Machine machine, CalibrationCertificate? current,
        bool renewalInProgress, DateOnly today)
    {
        if (!machine.CalibrationRequired)
            return new(machine.Id, false, CalibrationValidityStatus.NOT_REQUIRED,
                Renewal(renewalInProgress), current?.Id, current?.CertificateNumber, current?.ExpiryDate,
                current is null ? null : current.ExpiryDate.DayNumber - today.DayNumber);
        if (current is null)
            return new(machine.Id, true, CalibrationValidityStatus.EXPIRED, Renewal(renewalInProgress),
                null, null, null, null);
        var days = current.ExpiryDate.DayNumber - today.DayNumber;
        var validity = days <= 0 ? CalibrationValidityStatus.EXPIRED
            : days <= 60 ? CalibrationValidityStatus.EXPIRING_SOON : CalibrationValidityStatus.VALID;
        return new(machine.Id, true, validity, Renewal(renewalInProgress), current.Id,
            current.CertificateNumber, current.ExpiryDate, days);
    }

    private static CalibrationRenewalStatus Renewal(bool active) =>
        active ? CalibrationRenewalStatus.IN_PROGRESS : CalibrationRenewalStatus.NOT_STARTED;
}
