using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Breakdowns;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Reporting;

public sealed class ReportQueryService(IApplicationDbContext db, ICurrentUser currentUser,
    TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public Task<PagedResult<PreventiveMaintenanceReportRowDto>> PreventiveAsync(
        PreventiveMaintenanceReportQuery request, CancellationToken ct = default) =>
        PreventiveAsync(request, false, ct);

    internal async Task<PagedResult<PreventiveMaintenanceReportRowDto>> PreventiveAsync(
        PreventiveMaintenanceReportQuery request, bool exportAll, CancellationToken ct)
    {
        ReportingAccess.RequireReportAccess(currentUser);
        ValidatePage(request.Page, request.PageSize, exportAll);
        ValidateEnum(request.Status, "Status"); ValidateEnum(request.Priority, "Priority");
        var period = ReportingDefinitions.Period(request.From, request.To, Today);
        var query = ReportingAccess.VisibleWorkOrders(db, currentUser).AsNoTracking()
            .Where(x => x.MaintenancePlanId != null && x.DueDate >= period.From && x.DueDate <= period.To);
        query = FilterWorkOrders(query, request.MachineId, request.DepartmentId, request.LocationId,
            request.TechnicianId, request.SupervisorId);
        if (request.Status.HasValue) query = query.Where(x => x.LifecycleStatus == request.Status);
        if (request.Priority.HasValue) query = query.Where(x => x.Priority == request.Priority);
        var total = await query.CountAsync(ct);
        var projected = query.OrderBy(x => x.DueDate).ThenBy(x => x.WorkOrderNumber).Select(x =>
            new WorkOrderReportProjection(x.Id, x.WorkOrderNumber, x.MachineId,
                x.Definition.MachineCode, x.Definition.MachineName, x.Definition.PlanName,
                x.PlannedDate, x.DueDate, x.StartedAt, x.CompletedAt, x.SubmittedAt, x.ApprovedAt,
                x.AssignedTechnicianId, x.CurrentTechnicianName ?? x.Definition.AssignedTechnicianName,
                x.SupervisorId, x.Definition.SupervisorName, x.LifecycleStatus, x.Priority,
                x.EscalationLevel));
        var rows = await MaterializeAsync(projected, request.Page, request.PageSize, total, exportAll, ct);
        var escalations = await LastEscalationsAsync(rows.Select(x => x.Id).ToArray(), ct);
        var items = rows.Select(x => new PreventiveMaintenanceReportRowDto(x.Id, x.Number,
            x.MachineId, x.MachineCode, x.MachineName, x.PlanName, x.PlannedDate, x.DueDate,
            x.StartedAt, x.CompletedAt, x.SubmittedAt, x.ApprovedAt, x.TechnicianId, x.Technician,
            x.SupervisorId, x.Supervisor, x.Status, x.Priority,
            ReportingDefinitions.DaysOverdue(x.Status, x.SubmittedAt, x.DueDate, Today),
            x.EscalationLevel, escalations.GetValueOrDefault(x.Id))).ToArray();
        return Page(items, request.Page, request.PageSize, total, exportAll);
    }

    public Task<PagedResult<OverdueMaintenanceReportRowDto>> OverdueAsync(
        OverdueMaintenanceReportQuery request, CancellationToken ct = default) =>
        OverdueAsync(request, false, ct);

    internal async Task<PagedResult<OverdueMaintenanceReportRowDto>> OverdueAsync(
        OverdueMaintenanceReportQuery request, bool exportAll, CancellationToken ct)
    {
        ReportingAccess.RequireReportAccess(currentUser);
        ValidatePage(request.Page, request.PageSize, exportAll);
        if (request.EscalationLevel is < 0 or > 3)
            throw new AppException(400, "EscalationLevel must be between 0 and 3.");
        var asOf = request.AsOf ?? Today;
        if (request.DueFrom > asOf) throw new AppException(400, "DueFrom cannot follow AsOf.");
        var query = ReportingAccess.VisibleWorkOrders(db, currentUser).AsNoTracking()
            .Where(x => x.MaintenancePlanId != null && x.DueDate < asOf && x.SubmittedAt == null &&
                x.LifecycleStatus != WorkOrderLifecycleStatus.APPROVED &&
                x.LifecycleStatus != WorkOrderLifecycleStatus.CANCELLED &&
                x.LifecycleStatus != WorkOrderLifecycleStatus.AWAITING_APPROVAL);
        if (request.DueFrom.HasValue) query = query.Where(x => x.DueDate >= request.DueFrom);
        query = FilterWorkOrders(query, request.MachineId, request.DepartmentId, request.LocationId,
            request.TechnicianId, request.SupervisorId);
        if (request.EscalationLevel.HasValue)
            query = query.Where(x => x.EscalationLevel == request.EscalationLevel);
        var total = await query.CountAsync(ct);
        var projected = query.OrderBy(x => x.DueDate).ThenBy(x => x.WorkOrderNumber).Select(x =>
            new WorkOrderReportProjection(x.Id, x.WorkOrderNumber, x.MachineId,
                x.Definition.MachineCode, x.Definition.MachineName, x.Definition.PlanName,
                x.PlannedDate, x.DueDate, x.StartedAt, x.CompletedAt, x.SubmittedAt, x.ApprovedAt,
                x.AssignedTechnicianId, x.CurrentTechnicianName ?? x.Definition.AssignedTechnicianName,
                x.SupervisorId, x.Definition.SupervisorName, x.LifecycleStatus, x.Priority,
                x.EscalationLevel));
        var rows = await MaterializeAsync(projected, request.Page, request.PageSize, total, exportAll, ct);
        var escalations = await LastEscalationsAsync(rows.Select(x => x.Id).ToArray(), ct);
        var items = rows.Select(x => new OverdueMaintenanceReportRowDto(x.Id, x.Number,
            x.MachineId, x.MachineCode, x.MachineName, x.TechnicianId, x.Technician,
            x.SupervisorId, x.Supervisor, x.DueDate, x.Status,
            asOf.DayNumber - x.DueDate.DayNumber, x.EscalationLevel,
            escalations.GetValueOrDefault(x.Id), x.SubmittedAt.HasValue ? "SUBMITTED" : "NOT_SUBMITTED"))
            .ToArray();
        return Page(items, request.Page, request.PageSize, total, exportAll);
    }

    public Task<PagedResult<BreakdownReportRowDto>> BreakdownsAsync(BreakdownReportQuery request,
        CancellationToken ct = default) => BreakdownsAsync(request, false, ct);

    internal async Task<PagedResult<BreakdownReportRowDto>> BreakdownsAsync(BreakdownReportQuery request,
        bool exportAll, CancellationToken ct)
    {
        ReportingAccess.RequireReportAccess(currentUser);
        ValidatePage(request.Page, request.PageSize, exportAll);
        ValidateEnum(request.Severity, "Severity"); ValidateEnum(request.Status, "Status");
        var period = ReportingDefinitions.Period(request.From, request.To, Today);
        var bounds = ReportingDefinitions.UtcBounds(period.From, period.To);
        var query = ReportingAccess.VisibleBreakdowns(db, currentUser).AsNoTracking()
            .Where(x => x.ReportedAt >= bounds.From && x.ReportedAt < bounds.ToExclusive);
        if (request.MachineId.HasValue) query = query.Where(x => x.MachineId == request.MachineId);
        if (request.DepartmentId.HasValue) query = query.Where(x => x.Machine.DepartmentId == request.DepartmentId);
        if (request.LocationId.HasValue) query = query.Where(x => x.Machine.LocationId == request.LocationId);
        if (request.Severity.HasValue) query = query.Where(x => x.Severity == request.Severity);
        if (request.TechnicianId.HasValue) query = query.Where(x => x.AssignedTechnicianId == request.TechnicianId);
        if (request.SupervisorId.HasValue) query = query.Where(x => x.SupervisorId == request.SupervisorId);
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status);
        if (request.MachineStopped.HasValue) query = query.Where(x => x.MachineStopped == request.MachineStopped);
        var total = await query.CountAsync(ct);
        var projected = query.OrderByDescending(x => x.ReportedAt).ThenBy(x => x.BreakdownNumber)
            .Select(x => new BreakdownProjection(x.Id, x.BreakdownNumber, x.MachineId,
                x.MachineCode, x.MachineName, x.ReportedAt, x.Severity, x.MachineStopped,
                x.ReturnedToServiceAt, x.AssignedTechnicianId, x.TechnicianName,
                x.SupervisorId, x.SupervisorName, x.Status));
        var rows = await MaterializeAsync(projected, request.Page, request.PageSize, total, exportAll, ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var submissions = await db.CorrectiveSubmissions.AsNoTracking()
            .Where(x => ids.Contains(x.BreakdownId) && x.Review != null &&
                x.Review.Decision == CorrectiveReviewDecision.APPROVED)
            .Select(x => new { x.BreakdownId, x.VersionNumber, x.RootCause, x.CorrectiveAction })
            .ToListAsync(ct);
        var corrections = submissions.GroupBy(x => x.BreakdownId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.VersionNumber).First());
        var now = clock.GetUtcNow().UtcDateTime;
        var items = rows.Select(x =>
        {
            corrections.TryGetValue(x.Id, out var correction);
            var downtime = x.MachineStopped
                ? Math.Max(0, decimal.Round((decimal)((x.ReturnedToServiceAt ?? now) - x.ReportedAt).TotalMinutes, 2))
                : 0;
            return new BreakdownReportRowDto(x.Id, x.Number, x.MachineId, x.MachineCode,
                x.MachineName, x.ReportedAt, x.Severity, x.MachineStopped, downtime,
                correction?.RootCause, correction?.CorrectiveAction, x.TechnicianId,
                x.Technician, x.SupervisorId, x.Supervisor, x.Status, x.ReturnedToServiceAt);
        }).ToArray();
        return Page(items, request.Page, request.PageSize, total, exportAll);
    }

    public Task<PagedResult<CalibrationReportRowDto>> CalibrationAsync(CalibrationReportQuery request,
        CancellationToken ct = default) => CalibrationAsync(request, false, ct);

    internal async Task<PagedResult<CalibrationReportRowDto>> CalibrationAsync(
        CalibrationReportQuery request, bool exportAll, CancellationToken ct)
    {
        ReportingAccess.RequireReportAccess(currentUser);
        ValidatePage(request.Page, request.PageSize, exportAll);
        ValidateEnum(request.ValidityStatus, "ValidityStatus");
        ValidateEnum(request.RenewalStatus, "RenewalStatus");
        if (request.ExpiryFrom > request.ExpiryTo)
            throw new AppException(400, "ExpiryFrom cannot follow ExpiryTo.");
        var machineQuery = ReportingAccess.ApplyMachineFilters(
            ReportingAccess.VisibleMachines(db, currentUser).AsNoTracking().Where(x => x.IsActive),
            request.DepartmentId, request.LocationId);
        if (request.MachineId.HasValue) machineQuery = machineQuery.Where(x => x.Id == request.MachineId);
        var machines = await machineQuery.Select(x => new MachineProjection(x.Id, x.MachineCode,
            x.Name, x.CalibrationRequired)).ToListAsync(ct);
        var ids = machines.Select(x => x.Id).ToArray();
        var certificates = await db.CalibrationCertificates.AsNoTracking()
            .Where(x => ids.Contains(x.MachineId) && x.CalibrationDate <= Today && x.Result != CalibrationResult.FAIL)
            .Select(x => new CalibrationProjection(x.Id, x.MachineId, x.CertificateNumber,
                x.CalibrationProvider, x.CalibrationDate, x.ExpiryDate, x.Result, x.CreatedAt))
            .ToListAsync(ct);
        var current = certificates.GroupBy(x => x.MachineId).ToDictionary(x => x.Key,
            x => x.OrderByDescending(y => y.CalibrationDate).ThenByDescending(y => y.CreatedAt)
                .ThenByDescending(y => y.Id).First());
        var activeRenewals = (await db.CalibrationRenewals.AsNoTracking()
            .Where(x => ids.Contains(x.MachineId) && x.Status == CalibrationRenewalStatus.IN_PROGRESS)
            .Select(x => x.MachineId).ToListAsync(ct)).ToHashSet();
        var result = machines.Select(machine =>
        {
            current.TryGetValue(machine.Id, out var certificate);
            var validity = !machine.CalibrationRequired ? CalibrationValidityStatus.NOT_REQUIRED
                : certificate is null ? CalibrationValidityStatus.EXPIRED
                : certificate.ExpiryDate.DayNumber - Today.DayNumber <= 0 ? CalibrationValidityStatus.EXPIRED
                : certificate.ExpiryDate.DayNumber - Today.DayNumber <= ReportingDefinitions.ExpiringCalibrationDays
                    ? CalibrationValidityStatus.EXPIRING_SOON : CalibrationValidityStatus.VALID;
            return new CalibrationReportRowDto(machine.Id, machine.Code, machine.Name,
                certificate?.Id, certificate?.Number, certificate?.Provider,
                certificate?.CalibrationDate, certificate?.ExpiryDate,
                certificate is null ? null : certificate.ExpiryDate.DayNumber - Today.DayNumber,
                certificate?.Result, validity, activeRenewals.Contains(machine.Id)
                    ? CalibrationRenewalStatus.IN_PROGRESS : CalibrationRenewalStatus.NOT_STARTED);
        });
        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            var provider = request.Provider.Trim();
            result = result.Where(x => x.Provider?.Contains(provider, StringComparison.OrdinalIgnoreCase) == true);
        }
        if (request.ValidityStatus.HasValue) result = result.Where(x => x.ValidityStatus == request.ValidityStatus);
        if (request.RenewalStatus.HasValue) result = result.Where(x => x.RenewalStatus == request.RenewalStatus);
        if (request.ExpiryFrom.HasValue) result = result.Where(x => x.ExpiryDate >= request.ExpiryFrom);
        if (request.ExpiryTo.HasValue) result = result.Where(x => x.ExpiryDate <= request.ExpiryTo);
        var filtered = result.OrderBy(x => x.ExpiryDate ?? DateOnly.MaxValue).ThenBy(x => x.MachineCode).ToArray();
        EnsureExportLimit(filtered.Length, exportAll);
        var page = exportAll ? filtered : filtered.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArray();
        return Page(page, request.Page, request.PageSize, filtered.Length, exportAll);
    }

    public Task<PagedResult<TechnicianWorkloadRowDto>> TechnicianWorkloadAsync(
        TechnicianWorkloadReportQuery request, CancellationToken ct = default) =>
        TechnicianWorkloadAsync(request, false, ct);

    internal async Task<PagedResult<TechnicianWorkloadRowDto>> TechnicianWorkloadAsync(
        TechnicianWorkloadReportQuery request, bool exportAll, CancellationToken ct)
    {
        ReportingAccess.RequireReportAccess(currentUser);
        ValidatePage(request.Page, request.PageSize, exportAll);
        var period = ReportingDefinitions.Period(request.From, request.To, Today);
        var bounds = ReportingDefinitions.UtcBounds(period.From, period.To);
        var workOrders = ReportingAccess.VisibleWorkOrders(db, currentUser).AsNoTracking();
        workOrders = FilterWorkOrders(workOrders, null, request.DepartmentId, request.LocationId,
            request.TechnicianId, null);
        var orders = await workOrders.Where(x =>
                (x.DueDate >= period.From && x.DueDate <= period.To) ||
                (x.StartedAt >= bounds.From && x.StartedAt < bounds.ToExclusive) ||
                (x.ApprovedAt >= bounds.From && x.ApprovedAt < bounds.ToExclusive))
            .Select(x => new WorkloadOrder(x.Id, x.AssignedTechnicianId, x.DueDate,
                x.LifecycleStatus, x.SubmittedAt, x.StartedAt, x.ApprovedAt)).ToListAsync(ct);
        var workOrderIds = orders.Select(x => x.Id).ToArray();
        var submissions = await db.WorkOrderSubmissions.AsNoTracking()
            .Where(x => workOrderIds.Contains(x.WorkOrderId) && x.CompletedAt >= bounds.From &&
                x.CompletedAt < bounds.ToExclusive)
            .Select(x => new WorkloadSubmission(x.SubmittedByUserId, x.DurationMinutes)).ToListAsync(ct);
        var rejections = await db.WorkOrderApprovals.AsNoTracking()
            .Where(x => workOrderIds.Contains(x.WorkOrderId) &&
                x.Decision == WorkOrderReviewDecision.REJECTED && x.DecisionAt >= bounds.From &&
                x.DecisionAt < bounds.ToExclusive)
            .Select(x => x.Submission.SubmittedByUserId).ToListAsync(ct);
        var breakdownQuery = ReportingAccess.VisibleBreakdowns(db, currentUser).AsNoTracking()
            .Where(x => x.ReportedAt >= bounds.From && x.ReportedAt < bounds.ToExclusive);
        if (request.DepartmentId.HasValue) breakdownQuery = breakdownQuery.Where(x => x.Machine.DepartmentId == request.DepartmentId);
        if (request.LocationId.HasValue) breakdownQuery = breakdownQuery.Where(x => x.Machine.LocationId == request.LocationId);
        if (request.TechnicianId.HasValue) breakdownQuery = breakdownQuery.Where(x => x.AssignedTechnicianId == request.TechnicianId);
        var breakdowns = await breakdownQuery.Select(x => new WorkloadBreakdown(x.AssignedTechnicianId,
            x.Status, x.ClosedAt)).ToListAsync(ct);
        var technicianIds = orders.Where(x => x.TechnicianId.HasValue).Select(x => x.TechnicianId!.Value)
            .Concat(submissions.Select(x => x.TechnicianId)).Concat(rejections)
            .Concat(breakdowns.Where(x => x.TechnicianId.HasValue).Select(x => x.TechnicianId!.Value))
            .Distinct().ToArray();
        if (request.TechnicianId.HasValue && !technicianIds.Contains(request.TechnicianId.Value))
            technicianIds = technicianIds.Append(request.TechnicianId.Value).ToArray();
        var users = await db.Users.AsNoTracking().Where(x => technicianIds.Contains(x.Id))
            .Select(x => new { x.Id, x.EmployeeId, Name = x.FirstName + " " + x.LastName }).ToListAsync(ct);
        var rows = users.Select(user =>
        {
            var userOrders = orders.Where(x => x.TechnicianId == user.Id).ToArray();
            var durations = submissions.Where(x => x.TechnicianId == user.Id).Select(x => x.DurationMinutes).ToArray();
            return new TechnicianWorkloadRowDto(user.Id, user.EmployeeId, user.Name,
                userOrders.Count(x => x.DueDate >= period.From && x.DueDate <= period.To &&
                    x.Status != WorkOrderLifecycleStatus.CANCELLED),
                userOrders.Count(x => x.StartedAt >= bounds.From && x.StartedAt < bounds.ToExclusive),
                userOrders.Count(x => x.ApprovedAt >= bounds.From && x.ApprovedAt < bounds.ToExclusive),
                rejections.Count(x => x == user.Id),
                userOrders.Count(x => x.DueDate >= period.From && x.DueDate <= period.To &&
                    x.Status == WorkOrderLifecycleStatus.AWAITING_APPROVAL),
                userOrders.Count(x => x.DueDate >= period.From && x.DueDate <= period.To &&
                    ReportingDefinitions.IsOverdue(x.Status, x.SubmittedAt, x.DueDate, Today)),
                durations.Length == 0 ? null : decimal.Round(durations.Average(), 2),
                breakdowns.Count(x => x.TechnicianId == user.Id),
                breakdowns.Count(x => x.TechnicianId == user.Id && x.Status == BreakdownStatus.CLOSED &&
                    x.ClosedAt >= bounds.From && x.ClosedAt < bounds.ToExclusive));
        }).OrderBy(x => x.EmployeeId).ThenBy(x => x.TechnicianId).ToArray();
        EnsureExportLimit(rows.Length, exportAll);
        var selected = exportAll ? rows : rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArray();
        return Page(selected, request.Page, request.PageSize, rows.Length, exportAll);
    }

    public Task<PagedResult<ExternalServiceReportRowDto>> ExternalServicesAsync(
        ExternalServiceReportQuery request, CancellationToken ct = default) =>
        ExternalServicesAsync(request, false, ct);

    internal async Task<PagedResult<ExternalServiceReportRowDto>> ExternalServicesAsync(
        ExternalServiceReportQuery request, bool exportAll, CancellationToken ct)
    {
        ReportingAccess.RequireReportAccess(currentUser);
        ValidatePage(request.Page, request.PageSize, exportAll);
        ValidateEnum(request.ServiceType, "ServiceType"); ValidateEnum(request.FollowUpStatus, "FollowUpStatus");
        var period = ReportingDefinitions.Period(request.From, request.To, Today);
        var query = ReportingAccess.VisibleExternalServices(db, currentUser).AsNoTracking()
            .Where(x => x.ServiceDate >= period.From && x.ServiceDate <= period.To);
        if (request.MachineId.HasValue) query = query.Where(x => x.MachineId == request.MachineId);
        if (request.DepartmentId.HasValue) query = query.Where(x => x.Machine.DepartmentId == request.DepartmentId);
        if (request.LocationId.HasValue) query = query.Where(x => x.Machine.LocationId == request.LocationId);
        if (!string.IsNullOrWhiteSpace(request.Company))
        {
            var company = request.Company.Trim().ToUpperInvariant();
            query = query.Where(x => x.ServiceCompany.ToUpper().Contains(company));
        }
        if (request.ServiceType.HasValue) query = query.Where(x => x.ServiceType == request.ServiceType);
        if (request.FollowUpStatus.HasValue)
        {
            query = request.FollowUpStatus.Value switch
            {
                ExternalServiceFollowUpStatus.NONE => query.Where(x => x.FollowUpDate == null && x.NextServiceDate == null),
                ExternalServiceFollowUpStatus.OVERDUE => query.Where(x =>
                    (x.FollowUpDate.HasValue && x.FollowUpDate < Today) ||
                    (x.NextServiceDate.HasValue && x.NextServiceDate < Today)),
                ExternalServiceFollowUpStatus.DUE => query.Where(x =>
                    (x.FollowUpDate == Today || x.NextServiceDate == Today) &&
                    !(x.FollowUpDate.HasValue && x.FollowUpDate < Today) &&
                    !(x.NextServiceDate.HasValue && x.NextServiceDate < Today)),
                ExternalServiceFollowUpStatus.UPCOMING => query.Where(x =>
                    (x.FollowUpDate.HasValue || x.NextServiceDate.HasValue) &&
                    (!x.FollowUpDate.HasValue || x.FollowUpDate > Today) &&
                    (!x.NextServiceDate.HasValue || x.NextServiceDate > Today)),
                _ => query
            };
        }
        var total = await query.CountAsync(ct);
        var projected = query.OrderByDescending(x => x.ServiceDate).ThenBy(x => x.ServiceNumber)
            .Select(x => new ExternalProjection(x.Id, x.ServiceNumber, x.MachineId, x.MachineCode,
                x.MachineName, x.ServiceCompany, x.ServiceTechnician, x.ServiceDate,
                x.ServiceType, x.Findings, x.Cost, x.PurchaseOrderNumber, x.InvoiceNumber,
                x.FollowUpDate, x.NextServiceDate, x.Attachments.Count(a => a.IsActive)));
        var rows = await MaterializeAsync(projected, request.Page, request.PageSize, total, exportAll, ct);
        var showCost = ReportingAccess.CanViewFinancials(currentUser);
        var items = rows.Select(x => new ExternalServiceReportRowDto(x.Id, x.Number,
            x.MachineId, x.MachineCode, x.MachineName, x.Company, x.Technician,
            x.ServiceDate, x.ServiceType, x.Findings, showCost ? x.Cost : null,
            x.PurchaseOrderNumber, x.InvoiceNumber, x.FollowUpDate, x.NextServiceDate,
            ReportingDefinitions.FollowUpStatus(x.FollowUpDate, x.NextServiceDate, Today),
            x.AttachmentCount)).ToArray();
        return Page(items, request.Page, request.PageSize, total, exportAll);
    }

    public async Task<MaintenanceComplianceDto> ComplianceAsync(DateOnly from, DateOnly to,
        Guid? departmentId, Guid? locationId, CancellationToken ct = default)
    {
        var query = ReportingAccess.VisibleWorkOrders(db, currentUser).AsNoTracking()
            .Where(x => x.DueDate >= from && x.DueDate <= to);
        query = FilterWorkOrders(query, null, departmentId, locationId, null, null);
        var rows = await query.Select(x => new ComplianceWorkOrder(x.MaintenancePlanId != null,
            x.LifecycleStatus, x.SubmittedAt, x.DueDate)).ToListAsync(ct);
        return ReportingDefinitions.Compliance(rows, Today);
    }

    private IQueryable<Domain.Entities.WorkOrder> FilterWorkOrders(
        IQueryable<Domain.Entities.WorkOrder> query, Guid? machineId, Guid? departmentId,
        Guid? locationId, Guid? technicianId, Guid? supervisorId)
    {
        if (machineId.HasValue) query = query.Where(x => x.MachineId == machineId);
        if (departmentId.HasValue) query = query.Where(x => x.Machine.DepartmentId == departmentId);
        if (locationId.HasValue) query = query.Where(x => x.Machine.LocationId == locationId);
        if (technicianId.HasValue) query = query.Where(x => x.AssignedTechnicianId == technicianId);
        if (supervisorId.HasValue) query = query.Where(x => x.SupervisorId == supervisorId);
        return query;
    }

    private async Task<Dictionary<Guid, DateTime?>> LastEscalationsAsync(Guid[] ids,
        CancellationToken ct) => await db.WorkOrderEscalations.AsNoTracking()
        .Where(x => ids.Contains(x.WorkOrderId)).GroupBy(x => x.WorkOrderId)
        .Select(x => new { Id = x.Key, Last = (DateTime?)x.Max(y => y.TriggeredAt) })
        .ToDictionaryAsync(x => x.Id, x => x.Last, ct);

    private static async Task<List<T>> MaterializeAsync<T>(IQueryable<T> query, int page,
        int pageSize, int total, bool exportAll, CancellationToken ct)
    {
        EnsureExportLimit(total, exportAll);
        return exportAll
            ? await query.Take(ReportingDefinitions.ExportRowLimit).ToListAsync(ct)
            : await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    }

    private static PagedResult<T> Page<T>(IReadOnlyList<T> items, int page, int pageSize,
        int total, bool exportAll) => new(items, exportAll ? 1 : page,
            exportAll ? Math.Max(1, items.Count) : pageSize, total);

    private static void ValidatePage(int page, int pageSize, bool exportAll)
    {
        if (!exportAll) Guard.Page(page, pageSize);
    }

    private static void EnsureExportLimit(int total, bool exportAll)
    {
        if (exportAll && total > ReportingDefinitions.ExportRowLimit)
            throw new AppException(400, $"The export contains more than {ReportingDefinitions.ExportRowLimit} rows; narrow the filters.");
    }

    private static void ValidateEnum<T>(T? value, string name) where T : struct, Enum
    {
        if (value.HasValue && !Enum.IsDefined(value.Value))
            throw new AppException(400, $"{name} is invalid.");
    }

    private sealed record WorkOrderReportProjection(Guid Id, string Number, Guid MachineId,
        string MachineCode, string MachineName, string PlanName, DateOnly PlannedDate,
        DateOnly DueDate, DateTime? StartedAt, DateTime? CompletedAt, DateTime? SubmittedAt,
        DateTime? ApprovedAt, Guid? TechnicianId, string? Technician, Guid SupervisorId,
        string Supervisor, WorkOrderLifecycleStatus Status, MaintenancePriority Priority,
        int EscalationLevel);
    private sealed record BreakdownProjection(Guid Id, string Number, Guid MachineId,
        string MachineCode, string MachineName, DateTime ReportedAt, BreakdownSeverity Severity,
        bool MachineStopped, DateTime? ReturnedToServiceAt, Guid? TechnicianId,
        string? Technician, Guid SupervisorId, string Supervisor, BreakdownStatus Status);
    private sealed record MachineProjection(Guid Id, string Code, string Name, bool CalibrationRequired);
    private sealed record CalibrationProjection(Guid Id, Guid MachineId, string Number,
        string Provider, DateOnly CalibrationDate, DateOnly ExpiryDate,
        CalibrationResult Result, DateTime CreatedAt);
    private sealed record WorkloadOrder(Guid Id, Guid? TechnicianId, DateOnly DueDate,
        WorkOrderLifecycleStatus Status, DateTime? SubmittedAt, DateTime? StartedAt, DateTime? ApprovedAt);
    private sealed record WorkloadSubmission(Guid TechnicianId, decimal DurationMinutes);
    private sealed record WorkloadBreakdown(Guid? TechnicianId, BreakdownStatus Status, DateTime? ClosedAt);
    private sealed record ExternalProjection(Guid Id, string Number, Guid MachineId,
        string MachineCode, string MachineName, string Company, string? Technician,
        DateOnly ServiceDate, ExternalServiceType ServiceType, string? Findings, decimal? Cost,
        string? PurchaseOrderNumber, string? InvoiceNumber, DateOnly? FollowUpDate,
        DateOnly? NextServiceDate, int AttachmentCount);
}
