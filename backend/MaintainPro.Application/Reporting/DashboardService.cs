using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Reporting;

public sealed class DashboardService(IApplicationDbContext db, ICurrentUser currentUser,
    TimeProvider clock, ReportQueryService reports)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public async Task<ManagementDashboardDto> ManagerAsync(DashboardQuery request,
        CancellationToken ct = default)
    {
        ReportingAccess.RequireManagementDashboard(currentUser);
        return await ManagementAsync("MANAGER", request, ct);
    }

    public async Task<ManagementDashboardDto> SupervisorAsync(DashboardQuery request,
        CancellationToken ct = default)
    {
        ReportingAccess.RequireSupervisorDashboard(currentUser);
        return await ManagementAsync(ReportingAccess.IsManagement(currentUser) ? "MANAGER" : "SUPERVISOR",
            request, ct);
    }

    public async Task<TechnicianDashboardDto> TechnicianAsync(DashboardQuery request,
        CancellationToken ct = default)
    {
        ReportingAccess.RequireTechnicianDashboard(currentUser);
        var technicianId = Guard.Authenticated(currentUser);
        var period = ReportingDefinitions.Period(request.From, request.To, Today);
        var workOrders = db.WorkOrders.AsNoTracking().Where(x => x.AssignedTechnicianId == technicianId);
        if (request.DepartmentId.HasValue) workOrders = workOrders.Where(x => x.Machine.DepartmentId == request.DepartmentId);
        if (request.LocationId.HasValue) workOrders = workOrders.Where(x => x.Machine.LocationId == request.LocationId);
        var rows = await workOrders.Select(x => new TechnicianOrderProjection(x.DueDate,
            x.LifecycleStatus, x.SubmittedAt)).ToListAsync(ct);
        var upcomingTo = Today.AddDays(7);
        var dueToday = rows.Count(x => x.DueDate == Today && ReportingDefinitions.RequiresExecution(x.Status, x.SubmittedAt));
        var upcoming = rows.Count(x => x.DueDate > Today && x.DueDate <= upcomingTo &&
            ReportingDefinitions.RequiresExecution(x.Status, x.SubmittedAt));
        var overdue = rows.Count(x => ReportingDefinitions.IsOverdue(x.Status, x.SubmittedAt, x.DueDate, Today));
        var rejected = rows.Count(x => x.Status == WorkOrderLifecycleStatus.REJECTED);
        var inProgress = rows.Count(x => x.Status == WorkOrderLifecycleStatus.IN_PROGRESS);
        var unread = await db.Notifications.AsNoTracking().CountAsync(x => x.UserId == technicianId && !x.IsRead, ct);
        var breakdowns = await db.Breakdowns.AsNoTracking().CountAsync(x =>
            x.AssignedTechnicianId == technicianId && x.Status != BreakdownStatus.CLOSED &&
            x.Status != BreakdownStatus.CANCELLED &&
            (!request.DepartmentId.HasValue || x.Machine.DepartmentId == request.DepartmentId) &&
            (!request.LocationId.HasValue || x.Machine.LocationId == request.LocationId), ct);
        var machines = await db.Machines.AsNoTracking().CountAsync(x => x.IsActive &&
            x.MachineOwnerUserId == technicianId &&
            (!request.DepartmentId.HasValue || x.DepartmentId == request.DepartmentId) &&
            (!request.LocationId.HasValue || x.LocationId == request.LocationId), ct);
        return new(new(period.From, period.To, Today), dueToday, upcoming, overdue, rejected,
            inProgress, unread, breakdowns, machines);
    }

    private async Task<ManagementDashboardDto> ManagementAsync(string scope, DashboardQuery request,
        CancellationToken ct)
    {
        var period = ReportingDefinitions.Period(request.From, request.To, Today);
        var machineQuery = ReportingAccess.ApplyMachineFilters(
            ReportingAccess.VisibleMachines(db, currentUser).AsNoTracking().Where(x => x.IsActive),
            request.DepartmentId, request.LocationId);
        var machines = await machineQuery.Select(x => new DashboardMachine(x.Id, x.MachineCode,
            x.Name, x.Status, x.CalibrationRequired)).ToListAsync(ct);
        var machineIds = machines.Select(x => x.Id).ToArray();
        var machineSummary = new MachineKpiDto(machines.Count,
            machines.Count(x => x.Status == MachineStatus.Operational),
            machines.Count(x => x.Status == MachineStatus.UnderMaintenance),
            machines.Count(x => x.Status == MachineStatus.Breakdown),
            machines.Count(x => x.Status == MachineStatus.OutOfService));

        var endOfWeek = ReportingDefinitions.EndOfIsoWeek(Today);
        var workOrders = await ReportingAccess.VisibleWorkOrders(db, currentUser).AsNoTracking()
            .Where(x => machineIds.Contains(x.MachineId) &&
                ((x.SubmittedAt == null && x.DueDate <= endOfWeek &&
                  x.LifecycleStatus != WorkOrderLifecycleStatus.APPROVED &&
                  x.LifecycleStatus != WorkOrderLifecycleStatus.CANCELLED &&
                  x.LifecycleStatus != WorkOrderLifecycleStatus.AWAITING_APPROVAL) ||
                 x.LifecycleStatus == WorkOrderLifecycleStatus.AWAITING_APPROVAL ||
                 (x.EscalationLevel > 0 && x.LifecycleStatus != WorkOrderLifecycleStatus.APPROVED &&
                  x.LifecycleStatus != WorkOrderLifecycleStatus.CANCELLED)))
            .Select(x => new DashboardWorkOrder(x.Id, x.WorkOrderNumber, x.MachineId,
                x.Definition.MachineCode, x.Definition.MachineName, x.DueDate,
                x.LifecycleStatus, x.SubmittedAt, x.EscalationLevel)).ToListAsync(ct);
        var maintenance = new MaintenanceKpiDto(
            workOrders.Count(x => x.DueDate == Today && ReportingDefinitions.RequiresExecution(x.Status, x.SubmittedAt)),
            workOrders.Count(x => x.DueDate >= Today && x.DueDate <= endOfWeek &&
                ReportingDefinitions.RequiresExecution(x.Status, x.SubmittedAt)),
            workOrders.Count(x => ReportingDefinitions.IsOverdue(x.Status, x.SubmittedAt, x.DueDate, Today)),
            workOrders.Count(x => x.Status == WorkOrderLifecycleStatus.AWAITING_APPROVAL),
            workOrders.Count(x => x.EscalationLevel > 0 && x.Status is not WorkOrderLifecycleStatus.APPROVED
                and not WorkOrderLifecycleStatus.CANCELLED));

        var breakdownQuery = ReportingAccess.VisibleBreakdowns(db, currentUser).AsNoTracking()
            .Where(x => machineIds.Contains(x.MachineId) && x.Status != BreakdownStatus.CLOSED &&
                x.Status != BreakdownStatus.CANCELLED);
        var breakdownSummary = new BreakdownKpiDto(await breakdownQuery.CountAsync(ct),
            await breakdownQuery.CountAsync(x => x.Severity == BreakdownSeverity.CRITICAL, ct));

        var certificates = await db.CalibrationCertificates.AsNoTracking()
            .Where(x => machineIds.Contains(x.MachineId) && x.CalibrationDate <= Today &&
                x.Result != CalibrationResult.FAIL)
            .Select(x => new DashboardCertificate(x.Id, x.MachineId, x.CertificateNumber,
                x.CalibrationDate, x.ExpiryDate, x.CreatedAt)).ToListAsync(ct);
        var currentCertificates = certificates.GroupBy(x => x.MachineId).ToDictionary(x => x.Key,
            x => x.OrderByDescending(y => y.CalibrationDate).ThenByDescending(y => y.CreatedAt)
                .ThenByDescending(y => y.Id).First());
        var activeRenewals = (await db.CalibrationRenewals.AsNoTracking()
            .Where(x => machineIds.Contains(x.MachineId) && x.Status == CalibrationRenewalStatus.IN_PROGRESS)
            .Select(x => x.MachineId).ToListAsync(ct)).ToHashSet();
        var calibrationRows = machines.Where(x => x.CalibrationRequired).Select(machine =>
        {
            currentCertificates.TryGetValue(machine.Id, out var certificate);
            var days = certificate is null ? (int?)null : certificate.ExpiryDate.DayNumber - Today.DayNumber;
            var status = certificate is null || days <= 0 ? CalibrationValidityStatus.EXPIRED
                : days <= ReportingDefinitions.ExpiringCalibrationDays ? CalibrationValidityStatus.EXPIRING_SOON
                : CalibrationValidityStatus.VALID;
            return new DashboardCalibration(machine, certificate, days, status);
        }).ToArray();
        var calibration = new CalibrationKpiDto(
            calibrationRows.Count(x => x.Status == CalibrationValidityStatus.VALID),
            calibrationRows.Count(x => x.Days is > 0 and <= 60),
            calibrationRows.Count(x => x.Days is > 0 and <= 30),
            calibrationRows.Count(x => x.Days is > 0 and <= 7),
            calibrationRows.Count(x => x.Status == CalibrationValidityStatus.EXPIRED),
            activeRenewals.Count);

        var followUpQuery = ReportingAccess.VisibleExternalServices(db, currentUser).AsNoTracking()
            .Where(x => machineIds.Contains(x.MachineId) && (x.FollowUpDate.HasValue || x.NextServiceDate.HasValue));
        var followUpRows = await followUpQuery.Select(x => new DashboardFollowUp(x.Id,
            x.ServiceNumber, x.MachineId, x.MachineCode, x.MachineName, x.ServiceCompany,
            x.FollowUpDate, x.NextServiceDate, x.Recommendation)).ToListAsync(ct);
        var dueFollowUps = followUpRows.Where(x => Earliest(x.FollowUpDate, x.NextServiceDate) <= Today).ToArray();

        var escalationIds = workOrders.Where(x => x.EscalationLevel > 0 &&
            x.Status is not WorkOrderLifecycleStatus.APPROVED and not WorkOrderLifecycleStatus.CANCELLED)
            .Select(x => x.Id).ToArray();
        var lastEscalations = await db.WorkOrderEscalations.AsNoTracking()
            .Where(x => escalationIds.Contains(x.WorkOrderId)).GroupBy(x => x.WorkOrderId)
            .Select(x => new { Id = x.Key, Last = (DateTime?)x.Max(y => y.TriggeredAt) })
            .ToDictionaryAsync(x => x.Id, x => x.Last, ct);
        var escalationAlerts = workOrders.Where(x => escalationIds.Contains(x.Id))
            .OrderByDescending(x => x.EscalationLevel).ThenBy(x => x.DueDate).Take(20)
            .Select(x => new EscalationAlertDto(x.Id, x.Number, x.MachineId, x.MachineCode,
                x.MachineName, x.DueDate, x.EscalationLevel, lastEscalations.GetValueOrDefault(x.Id)))
            .ToArray();
        var upcomingCalibrations = calibrationRows.Where(x => x.Days is > 0 and <= 60)
            .OrderBy(x => x.Days).ThenBy(x => x.Machine.Code).Take(20)
            .Select(x => new UpcomingCalibrationDto(x.Machine.Id, x.Machine.Code, x.Machine.Name,
                x.Certificate?.Id, x.Certificate?.Number, x.Certificate?.ExpiryDate,
                x.Days, x.Status)).ToArray();
        var followUps = followUpRows.OrderBy(x => Earliest(x.FollowUpDate, x.NextServiceDate)).Take(20)
            .Select(x =>
            {
                var due = Earliest(x.FollowUpDate, x.NextServiceDate);
                return new ExternalServiceFollowUpSummaryDto(x.Id, x.Number, x.MachineId,
                    x.MachineCode, x.MachineName, x.Company, due, due < Today, x.Recommendation);
            }).ToArray();
        var compliance = await reports.ComplianceAsync(period.From, period.To,
            request.DepartmentId, request.LocationId, ct);
        var workload = await reports.TechnicianWorkloadAsync(new(period.From, period.To,
            DepartmentId: request.DepartmentId, LocationId: request.LocationId, PageSize: 100), ct);
        return new(scope, new(period.From, period.To, Today), machineSummary, maintenance,
            breakdownSummary, calibration, dueFollowUps.Length, compliance, workload.Items,
            escalationAlerts, upcomingCalibrations, followUps);
    }

    private static DateOnly Earliest(DateOnly? first, DateOnly? second) =>
        first.HasValue && second.HasValue ? (first < second ? first.Value : second.Value)
            : first ?? second ?? DateOnly.MaxValue;

    private sealed record TechnicianOrderProjection(DateOnly DueDate,
        WorkOrderLifecycleStatus Status, DateTime? SubmittedAt);
    private sealed record DashboardMachine(Guid Id, string Code, string Name,
        MachineStatus Status, bool CalibrationRequired);
    private sealed record DashboardWorkOrder(Guid Id, string Number, Guid MachineId,
        string MachineCode, string MachineName, DateOnly DueDate,
        WorkOrderLifecycleStatus Status, DateTime? SubmittedAt, int EscalationLevel);
    private sealed record DashboardCertificate(Guid Id, Guid MachineId, string Number,
        DateOnly CalibrationDate, DateOnly ExpiryDate, DateTime CreatedAt);
    private sealed record DashboardCalibration(DashboardMachine Machine,
        DashboardCertificate? Certificate, int? Days, CalibrationValidityStatus Status);
    private sealed record DashboardFollowUp(Guid Id, string Number, Guid MachineId,
        string MachineCode, string MachineName, string Company, DateOnly? FollowUpDate,
        DateOnly? NextServiceDate, string? Recommendation);
}
