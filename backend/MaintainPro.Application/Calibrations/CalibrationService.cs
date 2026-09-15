using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Calibrations;

public sealed class CalibrationService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock, IFileStorageService storage)
{
    private readonly CalibrationTimingService timing = new();

    public async Task<CalibrationCertificateDto> CreateAsync(CalibrationCreateRequest request,
        CalibrationFileUpload? upload = null, Stream? content = null, CancellationToken ct = default)
    {
        CalibrationAccess.RequireManager(currentUser);
        if (!Enum.IsDefined(request.Result)) throw new AppException(400, "Result is invalid.");
        if (request.ExpiryDate <= request.CalibrationDate) throw new AppException(400, "ExpiryDate must follow CalibrationDate.");
        var number = Guard.Required(request.CertificateNumber, "CertificateNumber", 200).ToUpperInvariant();
        var provider = Guard.Required(request.CalibrationProvider, "CalibrationProvider", 300).ToUpperInvariant();
        var remarks = Optional(request.Remarks, "Remarks", 10000);
        if ((upload is null) != (content is null)) throw new AppException(400, "Certificate file metadata and content must be provided together.");
        await using var transaction = await db.BeginTransactionAsync(ct);
        var machine = await db.Machines.SingleOrDefaultAsync(x => x.Id == request.MachineId, ct)
            ?? throw new AppException(404, "Machine not found.");
        if (!machine.CalibrationRequired && !request.RecordForNonRequiredMachine)
            throw new AppException(409, "This machine is not marked as requiring calibration. Set the explicit override to record history.");
        if (await db.CalibrationCertificates.AnyAsync(x => x.CalibrationProvider == provider && x.CertificateNumber == number, ct))
            throw new AppException(409, "Certificate number already exists for this provider.");
        StoredFile? stored = null; var commitAttempted = false;
        try
        {
            FileRecord? file = null;
            if (upload is not null && content is not null)
            {
                stored = await storage.StoreAsync(content, upload.OriginalFilename, upload.MimeType, upload.FileSize, ct);
                file = new FileRecord { StorageKey = stored.StorageKey, OriginalFilename = stored.OriginalFilename,
                    MimeType = stored.MimeType, FileSize = stored.FileSize,
                    UploadedByUserId = currentUser.UserId!.Value, UploadedAt = clock.GetUtcNow().UtcDateTime };
                db.FileRecords.Add(file);
            }
            var now = clock.GetUtcNow().UtcDateTime;
            var certificate = new CalibrationCertificate { MachineId = machine.Id, CertificateNumber = number,
                CalibrationProvider = provider, CalibrationDate = request.CalibrationDate, ExpiryDate = request.ExpiryDate,
                Result = request.Result, Remarks = remarks, CertificateFileId = file?.Id, CertificateFile = file,
                CreatedByUserId = currentUser.UserId!.Value, CreatedAt = now, UpdatedAt = now };
            db.CalibrationCertificates.Add(certificate);
            audit.Record("CalibrationCertificate.Created", nameof(CalibrationCertificate), certificate.Id,
                newValues: new { machine.Id, certificate.CertificateNumber, certificate.CalibrationProvider,
                    certificate.CalibrationDate, certificate.ExpiryDate, certificate.Result, certificate.CertificateFileId });
            await db.SaveChangesAsync(ct);
            var dto = await ToDtoAsync(certificate, machine, ct);
            commitAttempted = true; await transaction.CommitAsync(ct); return dto;
        }
        catch
        {
            if (stored is not null && !commitAttempted) await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<PagedResult<CalibrationCertificateDto>> ListAsync(CalibrationQuery request, CancellationToken ct = default)
    {
        Guard.Page(request.Page, request.PageSize); ValidateQuery(request);
        var visibleMachines = CalibrationAccess.VisibleMachines(db, currentUser);
        var query = db.CalibrationCertificates.AsNoTracking().Where(x => visibleMachines.Any(m => m.Id == x.MachineId));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = Guard.Required(request.Search, "Search", 200).ToUpperInvariant();
            query = query.Where(x => x.CertificateNumber.ToUpper().Contains(search) || x.CalibrationProvider.ToUpper().Contains(search)
                || x.Machine.MachineCode.ToUpper().Contains(search) || x.Machine.Name.ToUpper().Contains(search));
        }
        if (request.MachineId.HasValue) query = query.Where(x => x.MachineId == request.MachineId);
        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            var provider = request.Provider.Trim().ToUpperInvariant(); query = query.Where(x => x.CalibrationProvider.Contains(provider));
        }
        if (request.Result.HasValue) query = query.Where(x => x.Result == request.Result);
        if (request.ExpiryFrom.HasValue) query = query.Where(x => x.ExpiryDate >= request.ExpiryFrom);
        if (request.ExpiryTo.HasValue) query = query.Where(x => x.ExpiryDate <= request.ExpiryTo);
        var today = Today;
        if (request.ExpiringWithinDays.HasValue)
        {
            var through = today.AddDays(request.ExpiringWithinDays.Value);
            query = query.Where(x => x.ExpiryDate >= today && x.ExpiryDate <= through);
        }
        if (request.ExpiredOnly == true) query = query.Where(x => x.ExpiryDate <= today);
        var rows = await query.Include(x => x.Machine).OrderByDescending(x => x.CalibrationDate)
            .ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
        var renewalMachines = (await db.CalibrationRenewals.AsNoTracking().Where(x => x.Status == CalibrationRenewalStatus.IN_PROGRESS)
            .Select(x => x.MachineId).ToListAsync(ct)).ToHashSet();
        var dtos = new List<CalibrationCertificateDto>();
        foreach (var row in rows)
        {
            var dto = ToDto(row, row.Machine, renewalMachines.Contains(row.MachineId));
            if (request.ValidityStatus.HasValue && dto.ValidityStatus != request.ValidityStatus) continue;
            if (request.RenewalStatus.HasValue && dto.RenewalStatus != request.RenewalStatus) continue;
            dtos.Add(dto);
        }
        var total = dtos.Count;
        return new(dtos.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArray(), request.Page, request.PageSize, total);
    }

    public async Task<CalibrationCertificateDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var visible = CalibrationAccess.VisibleMachines(db, currentUser);
        var cert = await db.CalibrationCertificates.AsNoTracking().Include(x => x.Machine)
            .SingleOrDefaultAsync(x => x.Id == id && visible.Any(m => m.Id == x.MachineId), ct)
            ?? throw new AppException(404, "Calibration certificate not found.");
        return ToDto(cert, cert.Machine, await ActiveRenewalAsync(cert.MachineId, ct));
    }

    public async Task<PagedResult<CalibrationCertificateDto>> MachineHistoryAsync(Guid machineId,
        CalibrationQuery request, CancellationToken ct = default)
    {
        if (!await CalibrationAccess.VisibleMachines(db, currentUser).AsNoTracking()
                .AnyAsync(x => x.Id == machineId, ct))
            throw new AppException(404, "Machine not found.");

        return await ListAsync(request with { MachineId = machineId }, ct);
    }

    public async Task<MachineCalibrationStatusDto> MachineStatusAsync(Guid machineId, CancellationToken ct = default)
    {
        var machine = await CalibrationAccess.VisibleMachines(db, currentUser).AsNoTracking().SingleOrDefaultAsync(x => x.Id == machineId, ct)
            ?? throw new AppException(404, "Machine not found.");
        var certs = await db.CalibrationCertificates.AsNoTracking().Where(x => x.MachineId == machineId).ToListAsync(ct);
        return timing.Evaluate(machine, timing.Current(certs, Today), await ActiveRenewalAsync(machineId, ct), Today);
    }

    public async Task<CalibrationSummaryDto> SummaryAsync(CancellationToken ct = default)
    {
        var machines = await CalibrationAccess.VisibleMachines(db, currentUser).AsNoTracking().Where(x => x.CalibrationRequired).ToListAsync(ct);
        var ids = machines.Select(x => x.Id).ToArray();
        var certs = await db.CalibrationCertificates.AsNoTracking().Where(x => ids.Contains(x.MachineId)).ToListAsync(ct);
        var renewals = (await db.CalibrationRenewals.AsNoTracking().Where(x => ids.Contains(x.MachineId) && x.Status == CalibrationRenewalStatus.IN_PROGRESS)
            .Select(x => x.MachineId).ToListAsync(ct)).ToHashSet();
        var states = machines.Select(m => timing.Evaluate(m, timing.Current(certs.Where(c => c.MachineId == m.Id), Today), renewals.Contains(m.Id), Today)).ToArray();
        return new(states.Length, states.Count(x => x.ValidityStatus == CalibrationValidityStatus.VALID),
            states.Count(x => x.DaysRemaining is > 0 and <= 60), states.Count(x => x.DaysRemaining is > 0 and <= 30),
            states.Count(x => x.DaysRemaining is > 0 and <= 7), states.Count(x => x.ValidityStatus == CalibrationValidityStatus.EXPIRED),
            states.Count(x => x.RenewalStatus == CalibrationRenewalStatus.IN_PROGRESS));
    }

    private async Task<CalibrationCertificateDto> ToDtoAsync(CalibrationCertificate c, Machine m, CancellationToken ct) =>
        ToDto(c, m, await ActiveRenewalAsync(m.Id, ct));
    private CalibrationCertificateDto ToDto(CalibrationCertificate c, Machine m, bool renewal)
    {
        var status = timing.Evaluate(m, c.Result == CalibrationResult.FAIL || c.CalibrationDate > Today ? null : c, renewal, Today);
        return new(c.Id, m.Id, m.MachineCode, m.Name, c.CertificateNumber, c.CalibrationProvider,
            c.CalibrationDate, c.ExpiryDate, c.Result, c.Remarks, c.CertificateFileId, c.CreatedByUserId,
            c.CreatedAt, c.ExpiryDate.DayNumber - Today.DayNumber, status.ValidityStatus, status.RenewalStatus);
    }
    private Task<bool> ActiveRenewalAsync(Guid id, CancellationToken ct) => db.CalibrationRenewals.AsNoTracking()
        .AnyAsync(x => x.MachineId == id && x.Status == CalibrationRenewalStatus.IN_PROGRESS, ct);
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    private static string? Optional(string? value, string name, int max) => string.IsNullOrWhiteSpace(value) ? null : Guard.Required(value, name, max);
    private static void ValidateQuery(CalibrationQuery q)
    {
        if (q.Result.HasValue && !Enum.IsDefined(q.Result.Value)) throw new AppException(400, "Result is invalid.");
        if (q.ValidityStatus.HasValue && !Enum.IsDefined(q.ValidityStatus.Value)) throw new AppException(400, "ValidityStatus is invalid.");
        if (q.RenewalStatus.HasValue && !Enum.IsDefined(q.RenewalStatus.Value)) throw new AppException(400, "RenewalStatus is invalid.");
        if (q.ExpiryFrom.HasValue && q.ExpiryTo.HasValue && q.ExpiryFrom > q.ExpiryTo) throw new AppException(400, "ExpiryFrom cannot follow ExpiryTo.");
        if (q.ExpiringWithinDays is < 0 or > 3650) throw new AppException(400, "ExpiringWithinDays must be between 0 and 3650.");
    }
}
