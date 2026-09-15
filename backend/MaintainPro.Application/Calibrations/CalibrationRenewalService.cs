using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Calibrations;

public sealed class CalibrationRenewalService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock)
{
    private readonly CalibrationTimingService timing = new();

    public async Task<CalibrationRenewalDto> StartAsync(Guid machineId, CalibrationRenewalStartRequest request, CancellationToken ct = default)
    {
        CalibrationAccess.RequireManager(currentUser);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var machine = await db.Machines.SingleOrDefaultAsync(x => x.Id == machineId, ct) ?? throw new AppException(404, "Machine not found.");
        if (!machine.CalibrationRequired) throw new AppException(409, "Calibration renewal is available only for machines that require calibration.");
        if (await db.CalibrationRenewals.AnyAsync(x => x.MachineId == machineId && x.Status == CalibrationRenewalStatus.IN_PROGRESS, ct))
            throw new AppException(409, "A calibration renewal is already in progress.");
        var certs = await db.CalibrationCertificates.AsNoTracking().Where(x => x.MachineId == machineId).ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var renewal = new CalibrationRenewal { MachineId = machineId,
            PreviousCertificateId = timing.Current(certs, DateOnly.FromDateTime(now))?.Id,
            Status = CalibrationRenewalStatus.IN_PROGRESS, StartedAt = now, StartedByUserId = currentUser.UserId!.Value,
            Notes = Optional(request.Notes), CreatedAt = now, UpdatedAt = now };
        db.CalibrationRenewals.Add(renewal);
        audit.Record("CalibrationRenewal.Started", nameof(CalibrationRenewal), renewal.Id,
            newValues: new { machineId, renewal.PreviousCertificateId, renewal.StartedAt, renewal.Notes });
        await new CalibrationNotificationService(db, audit, clock).EnsureAsync(machine,
            NotificationType.CALIBRATION_RENEWAL_STARTED, renewal: renewal, ct: ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return ToDto(renewal);
    }

    public async Task<CalibrationRenewalDto> CompleteAsync(Guid machineId, CalibrationRenewalCompleteRequest request, CancellationToken ct = default)
    {
        CalibrationAccess.RequireManager(currentUser);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var machine = await db.Machines.SingleOrDefaultAsync(x => x.Id == machineId, ct) ?? throw new AppException(404, "Machine not found.");
        var renewal = await db.CalibrationRenewals.SingleOrDefaultAsync(x => x.MachineId == machineId && x.Status == CalibrationRenewalStatus.IN_PROGRESS, ct)
            ?? throw new AppException(409, "No calibration renewal is in progress.");
        var certificate = await db.CalibrationCertificates.SingleOrDefaultAsync(x => x.Id == request.CertificateId && x.MachineId == machineId, ct)
            ?? throw new AppException(400, "The completion certificate must belong to this machine.");
        if (certificate.CreatedAt < renewal.StartedAt || certificate.Id == renewal.PreviousCertificateId)
            throw new AppException(409, "Renewal completion requires a newly recorded certificate.");
        if (await db.CalibrationRenewals.AnyAsync(x => x.CompletedCertificateId == certificate.Id, ct))
            throw new AppException(409, "This certificate already completed another renewal.");
        var now = clock.GetUtcNow().UtcDateTime;
        renewal.Status = CalibrationRenewalStatus.COMPLETED; renewal.CompletedAt = now;
        renewal.CompletedCertificateId = certificate.Id; renewal.Notes = Optional(request.Notes) ?? renewal.Notes;
        audit.Record("CalibrationRenewal.Completed", nameof(CalibrationRenewal), renewal.Id,
            newValues: new { certificate.Id, renewal.CompletedAt, renewal.Notes });
        await new CalibrationNotificationService(db, audit, clock).EnsureAsync(machine,
            NotificationType.CALIBRATION_RENEWED, certificate, renewal, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return ToDto(renewal);
    }

    public async Task<CalibrationRenewalDto> CancelAsync(Guid machineId, CalibrationRenewalCancelRequest request, CancellationToken ct = default)
    {
        CalibrationAccess.RequireManager(currentUser);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var renewal = await db.CalibrationRenewals.SingleOrDefaultAsync(x => x.MachineId == machineId && x.Status == CalibrationRenewalStatus.IN_PROGRESS, ct)
            ?? throw new AppException(409, "No calibration renewal is in progress.");
        renewal.Status = CalibrationRenewalStatus.CANCELLED; renewal.CompletedAt = clock.GetUtcNow().UtcDateTime;
        renewal.Notes = Optional(request.Notes) ?? renewal.Notes;
        audit.Record("CalibrationRenewal.Cancelled", nameof(CalibrationRenewal), renewal.Id,
            newValues: new { renewal.CompletedAt, renewal.Notes });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return ToDto(renewal);
    }

    public async Task<IReadOnlyList<CalibrationRenewalDto>> ListAsync(Guid machineId, CancellationToken ct = default)
    {
        if (!await CalibrationAccess.VisibleMachines(db, currentUser).AnyAsync(x => x.Id == machineId, ct)) throw new AppException(404, "Machine not found.");
        return await db.CalibrationRenewals.AsNoTracking().Where(x => x.MachineId == machineId)
            .OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.Id).Select(x =>
                new CalibrationRenewalDto(x.Id, x.MachineId, x.PreviousCertificateId, x.Status,
                    x.StartedAt, x.StartedByUserId, x.CompletedAt, x.CompletedCertificateId, x.Notes)).ToListAsync(ct);
    }

    private static CalibrationRenewalDto ToDto(CalibrationRenewal x) => new(x.Id, x.MachineId,
        x.PreviousCertificateId, x.Status, x.StartedAt, x.StartedByUserId, x.CompletedAt, x.CompletedCertificateId, x.Notes);
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : Guard.Required(value, "Notes", 10000);
}
