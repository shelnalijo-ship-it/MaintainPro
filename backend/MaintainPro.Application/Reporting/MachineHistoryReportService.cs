using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Reporting;

public sealed class MachineHistoryReportService(IApplicationDbContext db, ICurrentUser currentUser)
{
    private static readonly HashSet<string> Modules = new(StringComparer.OrdinalIgnoreCase)
    {
        "PREVENTIVE_MAINTENANCE", "BREAKDOWN", "CALIBRATION", "EXTERNAL_SERVICE",
        "MACHINE_DOCUMENT", "ASSIGNMENT", "MACHINE_STATUS"
    };

    public async Task<MachineHistoryReportDto> GetAsync(Guid machineId,
        MachineHistoryReportQuery request, CancellationToken ct = default)
    {
        Guard.Page(request.Page, request.PageSize);
        var loaded = await LoadAsync(machineId, request, ct);
        var page = loaded.Events.Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize).ToArray();
        return new(loaded.MachineId, loaded.MachineCode, loaded.MachineName,
            new(page, request.Page, request.PageSize, loaded.Events.Length));
    }

    private async Task<(Guid MachineId, string MachineCode, string MachineName,
        MachineHistoryEventDto[] Events)> LoadAsync(Guid machineId,
        MachineHistoryReportQuery request, CancellationToken ct)
    {
        if (request.From > request.To) throw new AppException(400, "From date cannot follow To date.");
        var module = string.IsNullOrWhiteSpace(request.Module) ? null : request.Module.Trim().ToUpperInvariant();
        if (module is not null && !Modules.Contains(module))
            throw new AppException(400, "Module is invalid.");
        var machine = await ReportingAccess.VisibleMachines(db, currentUser).AsNoTracking()
            .Where(x => x.Id == machineId)
            .Select(x => new { x.Id, x.MachineCode, x.Name }).SingleOrDefaultAsync(ct)
            ?? throw new AppException(404, "Machine not found.");
        var from = request.From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toExclusive = request.To?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var events = new List<MachineHistoryEventDto>();

        if (module is null or "PREVENTIVE_MAINTENANCE")
        {
            var query = db.WorkOrderHistoryEvents.AsNoTracking().Where(x => x.WorkOrder.MachineId == machineId);
            if (from.HasValue) query = query.Where(x => x.OccurredAt >= from);
            if (toExclusive.HasValue) query = query.Where(x => x.OccurredAt < toExclusive);
            events.AddRange(await query.Select(x => new MachineHistoryEventDto(
                "PREVENTIVE_MAINTENANCE", x.Action, x.OccurredAt, x.WorkOrderId,
                x.WorkOrder.WorkOrderNumber, x.Action + " for " + x.WorkOrder.Definition.PlanName,
                x.Details)).ToListAsync(ct));
        }

        if (module is null or "BREAKDOWN")
        {
            var query = db.BreakdownHistoryEvents.AsNoTracking().Where(x => x.Breakdown.MachineId == machineId);
            if (from.HasValue) query = query.Where(x => x.OccurredAt >= from);
            if (toExclusive.HasValue) query = query.Where(x => x.OccurredAt < toExclusive);
            events.AddRange(await query.Select(x => new MachineHistoryEventDto(
                "BREAKDOWN", x.Action, x.OccurredAt, x.BreakdownId,
                x.Breakdown.BreakdownNumber, x.Action + " for " + x.Breakdown.Description,
                x.Details)).ToListAsync(ct));
        }

        if (module is null or "CALIBRATION")
        {
            var certificates = db.CalibrationCertificates.AsNoTracking().Where(x => x.MachineId == machineId);
            if (from.HasValue) certificates = certificates.Where(x => x.CreatedAt >= from);
            if (toExclusive.HasValue) certificates = certificates.Where(x => x.CreatedAt < toExclusive);
            events.AddRange(await certificates.Select(x => new MachineHistoryEventDto(
                "CALIBRATION", "Calibration.CertificateRecorded", x.CreatedAt, x.Id,
                x.CertificateNumber, "Calibration certificate recorded by " + x.CalibrationProvider,
                "CalibrationDate=" + x.CalibrationDate + "; ExpiryDate=" + x.ExpiryDate +
                "; Result=" + x.Result)).ToListAsync(ct));

            var renewals = db.CalibrationRenewals.AsNoTracking().Where(x => x.MachineId == machineId);
            if (from.HasValue) renewals = renewals.Where(x => x.StartedAt >= from || x.CompletedAt >= from);
            if (toExclusive.HasValue) renewals = renewals.Where(x => x.StartedAt < toExclusive || x.CompletedAt < toExclusive);
            var renewalRows = await renewals.Select(x => new { x.Id, x.StartedAt, x.CompletedAt,
                x.Status, x.PreviousCertificateId, x.CompletedCertificateId, x.Notes }).ToListAsync(ct);
            foreach (var renewal in renewalRows)
            {
                if (Within(renewal.StartedAt, from, toExclusive))
                    events.Add(new("CALIBRATION", "Calibration.RenewalStarted", renewal.StartedAt,
                        renewal.Id, renewal.Id.ToString(), "Calibration renewal started",
                        "PreviousCertificateId=" + renewal.PreviousCertificateId));
                if (renewal.CompletedAt is DateTime completed && Within(completed, from, toExclusive))
                    events.Add(new("CALIBRATION", "Calibration.Renewal" + renewal.Status, completed,
                        renewal.Id, renewal.Id.ToString(), "Calibration renewal " + renewal.Status,
                        "CompletedCertificateId=" + renewal.CompletedCertificateId + "; Notes=" + renewal.Notes));
            }
        }

        if (module is null or "EXTERNAL_SERVICE")
        {
            var query = db.ExternalServices.AsNoTracking().Where(x => x.MachineId == machineId);
            if (from.HasValue) query = query.Where(x => x.CreatedAt >= from);
            if (toExclusive.HasValue) query = query.Where(x => x.CreatedAt < toExclusive);
            events.AddRange(await query.Select(x => new MachineHistoryEventDto(
                "EXTERNAL_SERVICE", "ExternalService.Recorded", x.CreatedAt, x.Id,
                x.ServiceNumber, x.ServiceType + " by " + x.ServiceCompany,
                "ServiceDate=" + x.ServiceDate + "; Technician=" + x.ServiceTechnician +
                "; Findings=" + x.Findings + "; WorkCompleted=" + x.WorkCompleted +
                "; PartsReplaced=" + x.PartsReplaced + "; FollowUpDate=" + x.FollowUpDate +
                "; NextServiceDate=" + x.NextServiceDate)).ToListAsync(ct));
        }

        Guid[] documentIds = [];
        if (module is null or "MACHINE_DOCUMENT")
        {
            var documents = await db.MachineDocuments.AsNoTracking().Where(x => x.MachineId == machineId)
                .Select(x => new { x.Id, x.Title, x.DocumentType, x.DocumentDate, x.ExpiryDate,
                    x.UploadedAt, x.UpdatedAt, x.IsActive }).ToListAsync(ct);
            documentIds = documents.Select(x => x.Id).ToArray();
            events.AddRange(documents.Where(x => Within(x.UploadedAt, from, toExclusive)).Select(x =>
                new MachineHistoryEventDto("MACHINE_DOCUMENT", "MachineDocument.Uploaded", x.UploadedAt,
                    x.Id, x.Title, x.DocumentType + " document uploaded",
                    "DocumentDate=" + x.DocumentDate + "; ExpiryDate=" + x.ExpiryDate +
                    "; IsActive=" + x.IsActive)));
            if (documentIds.Length > 0)
            {
                var idStrings = documentIds.Select(x => x.ToString()).ToArray();
                var audits = db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "MachineDocument" &&
                    x.EntityId != null && idStrings.Contains(x.EntityId) && x.Action != "MachineDocument.Uploaded");
                if (from.HasValue) audits = audits.Where(x => x.CreatedAt >= from);
                if (toExclusive.HasValue) audits = audits.Where(x => x.CreatedAt < toExclusive);
                var auditRows = await audits.Select(x => new { x.Action, x.CreatedAt,
                    x.EntityId, x.NewValuesJson }).ToListAsync(ct);
                events.AddRange(auditRows.Select(x => new MachineHistoryEventDto(
                    "MACHINE_DOCUMENT", x.Action, x.CreatedAt, Guid.Parse(x.EntityId!),
                    x.EntityId!, x.Action, x.NewValuesJson)));
            }
        }

        if (module is null or "ASSIGNMENT")
        {
            var query = db.MachineAssignmentHistories.AsNoTracking().Where(x => x.MachineId == machineId);
            if (from.HasValue) query = query.Where(x => x.EffectiveFrom >= from || x.EffectiveTo >= from);
            if (toExclusive.HasValue) query = query.Where(x => x.EffectiveFrom < toExclusive || x.EffectiveTo < toExclusive);
            var assignments = await query.Select(x => new { x.Id, x.EffectiveFrom, x.EffectiveTo,
                x.TechnicianId, Technician = x.Technician.FirstName + " " + x.Technician.LastName,
                x.SupervisorId, Supervisor = x.Supervisor == null ? null : x.Supervisor.FirstName + " " + x.Supervisor.LastName,
                x.AssignedByUserId, x.Reason }).ToListAsync(ct);
            events.AddRange(assignments.Where(x => Within(x.EffectiveFrom, from, toExclusive)).Select(x =>
                new MachineHistoryEventDto("ASSIGNMENT", "Machine.Assigned", x.EffectiveFrom,
                    x.Id, x.Technician, "Machine assigned to " + x.Technician,
                    "TechnicianId=" + x.TechnicianId + "; SupervisorId=" + x.SupervisorId +
                    "; Supervisor=" + x.Supervisor + "; AssignedByUserId=" + x.AssignedByUserId +
                    "; Reason=" + x.Reason + "; EffectiveTo=" + x.EffectiveTo)));
        }

        if (module is null or "MACHINE_STATUS")
        {
            var machineIdText = machineId.ToString();
            var query = db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "Machine" &&
                x.EntityId == machineIdText && x.Action == "Machine.StatusChanged");
            if (from.HasValue) query = query.Where(x => x.CreatedAt >= from);
            if (toExclusive.HasValue) query = query.Where(x => x.CreatedAt < toExclusive);
            events.AddRange(await query.Select(x => new MachineHistoryEventDto(
                "MACHINE_STATUS", x.Action, x.CreatedAt, machineId, machine.MachineCode,
                "Machine status changed", "Old=" + x.OldValuesJson + "; New=" + x.NewValuesJson))
                .ToListAsync(ct));
        }

        var ordered = events.OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Module)
            .ThenBy(x => x.SourceId).ToArray();
        return (machine.Id, machine.MachineCode, machine.Name, ordered);
    }

    internal async Task<MachineHistoryReportDto> ExportAsync(Guid machineId,
        MachineHistoryReportQuery request, CancellationToken ct)
    {
        var loaded = await LoadAsync(machineId, request, ct);
        if (loaded.Events.Length > ReportingDefinitions.ExportRowLimit)
            throw new AppException(400, $"The export contains more than {ReportingDefinitions.ExportRowLimit} rows; narrow the filters.");
        return new(loaded.MachineId, loaded.MachineCode, loaded.MachineName,
            new(loaded.Events, 1, Math.Max(1, loaded.Events.Length), loaded.Events.Length));
    }

    private static bool Within(DateTime value, DateTime? from, DateTime? toExclusive) =>
        (!from.HasValue || value >= from) && (!toExclusive.HasValue || value < toExclusive);
}
