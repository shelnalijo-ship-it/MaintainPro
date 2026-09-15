using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Reporting;

public sealed class SummaryReportingService(IApplicationDbContext db, ICurrentUser currentUser,
    TimeProvider clock, ReportQueryService reports)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public async Task<MonthlySummaryDto> MonthlyAsync(MonthlySummaryQuery request,
        CancellationToken ct = default)
    {
        ReportingAccess.RequireReportAccess(currentUser);
        ValidateMonth(request.Year, request.Month);
        var current = await MonthAsync(request.Year, request.Month, request.DepartmentId,
            request.LocationId, ct);
        var previousDate = new DateOnly(request.Year, request.Month, 1).AddMonths(-1);
        var previous = await MonthAsync(previousDate.Year, previousDate.Month,
            request.DepartmentId, request.LocationId, ct);
        return new(current, previous);
    }

    public async Task<ReportingTrendsDto> TrendsAsync(TrendQuery request,
        CancellationToken ct = default)
    {
        ReportingAccess.RequireReportAccess(currentUser);
        var defaultFrom = new DateOnly(Today.Year, Today.Month, 1).AddMonths(-11);
        var from = request.From ?? defaultFrom;
        var to = request.To ?? Today;
        if (from > to) throw new AppException(400, "From date cannot follow To date.");
        var monthCount = (to.Year - from.Year) * 12 + to.Month - from.Month + 1;
        if (monthCount > ReportingDefinitions.TrendMonthLimit)
            throw new AppException(400, $"Trend ranges cannot exceed {ReportingDefinitions.TrendMonthLimit} calendar months.");
        var bounds = ReportingDefinitions.UtcBounds(from, to);
        var workQuery = ReportingAccess.VisibleWorkOrders(db, currentUser).AsNoTracking()
            .Where(x => x.MaintenancePlanId != null && x.DueDate >= from && x.DueDate <= to);
        workQuery = FilterWorkOrders(workQuery, request.DepartmentId, request.LocationId);
        var work = await workQuery.Select(x => new TrendWorkOrder(x.DueDate, x.LifecycleStatus,
            x.SubmittedAt, x.ApprovedAt)).ToListAsync(ct);
        var breakdownQuery = ReportingAccess.VisibleBreakdowns(db, currentUser).AsNoTracking()
            .Where(x => x.ReportedAt >= bounds.From && x.ReportedAt < bounds.ToExclusive);
        if (request.DepartmentId.HasValue) breakdownQuery = breakdownQuery.Where(x => x.Machine.DepartmentId == request.DepartmentId);
        if (request.LocationId.HasValue) breakdownQuery = breakdownQuery.Where(x => x.Machine.LocationId == request.LocationId);
        var breakdowns = await breakdownQuery.Select(x => new TrendBreakdown(x.ReportedAt,
            x.MachineStopped, x.ReturnedToServiceAt)).ToListAsync(ct);
        var maintenance = new List<MaintenanceTrendPointDto>();
        var breakdownPoints = new List<BreakdownTrendPointDto>();
        var workload = new List<TechnicianWorkloadTrendPointDto>();
        var month = new DateOnly(from.Year, from.Month, 1);
        var lastMonth = new DateOnly(to.Year, to.Month, 1);
        var now = clock.GetUtcNow().UtcDateTime;
        while (month <= lastMonth)
        {
            var end = month.AddMonths(1).AddDays(-1);
            var monthWork = work.Where(x => x.DueDate >= month && x.DueDate <= end).ToArray();
            var compliance = ReportingDefinitions.Compliance(monthWork.Select(x =>
                new ComplianceWorkOrder(true, x.Status, x.SubmittedAt, x.DueDate)), Today);
            maintenance.Add(new(month.Year, month.Month, compliance.Percentage,
                compliance.CompletedCount, compliance.OverdueCount));
            workload.Add(new(month.Year, month.Month,
                monthWork.Count(x => x.Status != WorkOrderLifecycleStatus.CANCELLED),
                monthWork.Count(x => x.Status == WorkOrderLifecycleStatus.APPROVED),
                compliance.OverdueCount));
            var monthBreakdowns = breakdowns.Where(x => DateOnly.FromDateTime(x.ReportedAt) >= month &&
                DateOnly.FromDateTime(x.ReportedAt) <= end).ToArray();
            breakdownPoints.Add(new(month.Year, month.Month, monthBreakdowns.Length,
                monthBreakdowns.Sum(x => x.MachineStopped
                    ? Math.Max(0, decimal.Round((decimal)((x.ReturnedToServiceAt ?? now) - x.ReportedAt).TotalMinutes, 2))
                    : 0)));
            month = month.AddMonths(1);
        }
        var distribution = await CalibrationDistributionAsync(to, request.DepartmentId,
            request.LocationId, ct);
        return new(new(from, to, Today), maintenance, breakdownPoints, workload, distribution);
    }

    private async Task<MonthlyMetricsDto> MonthAsync(int year, int month, Guid? departmentId,
        Guid? locationId, CancellationToken ct)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var bounds = ReportingDefinitions.UtcBounds(from, to);
        var workQuery = ReportingAccess.VisibleWorkOrders(db, currentUser).AsNoTracking()
            .Where(x => x.MaintenancePlanId != null && x.DueDate >= from && x.DueDate <= to);
        workQuery = FilterWorkOrders(workQuery, departmentId, locationId);
        var work = await workQuery.Select(x => new TrendWorkOrder(x.DueDate, x.LifecycleStatus,
            x.SubmittedAt, x.ApprovedAt)).ToListAsync(ct);
        var compliance = ReportingDefinitions.Compliance(work.Select(x =>
            new ComplianceWorkOrder(true, x.Status, x.SubmittedAt, x.DueDate)), Today);
        var breakdownQuery = ReportingAccess.VisibleBreakdowns(db, currentUser).AsNoTracking()
            .Where(x => x.ReportedAt >= bounds.From && x.ReportedAt < bounds.ToExclusive);
        if (departmentId.HasValue) breakdownQuery = breakdownQuery.Where(x => x.Machine.DepartmentId == departmentId);
        if (locationId.HasValue) breakdownQuery = breakdownQuery.Where(x => x.Machine.LocationId == locationId);
        var breakdowns = await breakdownQuery.Select(x => new MonthlyBreakdown(x.ReportedAt,
            x.Severity, x.MachineStopped, x.ReturnedToServiceAt)).ToListAsync(ct);
        var externalQuery = ReportingAccess.VisibleExternalServices(db, currentUser).AsNoTracking()
            .Where(x => x.ServiceDate >= from && x.ServiceDate <= to);
        if (departmentId.HasValue) externalQuery = externalQuery.Where(x => x.Machine.DepartmentId == departmentId);
        if (locationId.HasValue) externalQuery = externalQuery.Where(x => x.Machine.LocationId == locationId);
        var externalCount = await externalQuery.CountAsync(ct);
        var escalationQuery = db.WorkOrderEscalations.AsNoTracking().Where(x =>
            x.TriggeredAt >= bounds.From && x.TriggeredAt < bounds.ToExclusive &&
            ReportingAccess.VisibleWorkOrders(db, currentUser).Any(order => order.Id == x.WorkOrderId));
        if (departmentId.HasValue) escalationQuery = escalationQuery.Where(x => x.WorkOrder.Machine.DepartmentId == departmentId);
        if (locationId.HasValue) escalationQuery = escalationQuery.Where(x => x.WorkOrder.Machine.LocationId == locationId);
        var escalationCount = await escalationQuery.CountAsync(ct);
        var calibration = await CalibrationDistributionAsync(to, departmentId, locationId, ct);
        var workload = await reports.TechnicianWorkloadAsync(new(from, to,
            DepartmentId: departmentId, LocationId: locationId), true, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        return new(year, month,
            work.Count(x => x.Status != WorkOrderLifecycleStatus.CANCELLED),
            work.Count(x => x.Status == WorkOrderLifecycleStatus.APPROVED),
            compliance.OverdueCount, compliance, breakdowns.Count,
            breakdowns.Count(x => x.Severity == BreakdownSeverity.CRITICAL),
            breakdowns.Sum(x => x.MachineStopped
                ? Math.Max(0, decimal.Round((decimal)((x.ReturnedToServiceAt ?? now) - x.ReportedAt).TotalMinutes, 2))
                : 0),
            externalCount,
            calibration.Single(x => x.Status == CalibrationValidityStatus.EXPIRED).Count,
            calibration.Single(x => x.Status == CalibrationValidityStatus.EXPIRING_SOON).Count,
            escalationCount, workload.Items);
    }

    private async Task<IReadOnlyList<CalibrationStatusDistributionDto>> CalibrationDistributionAsync(
        DateOnly asOf, Guid? departmentId, Guid? locationId, CancellationToken ct)
    {
        var machines = await ReportingAccess.ApplyMachineFilters(
                ReportingAccess.VisibleMachines(db, currentUser).AsNoTracking().Where(x => x.IsActive),
                departmentId, locationId)
            .Select(x => new { x.Id, x.CalibrationRequired }).ToListAsync(ct);
        var ids = machines.Select(x => x.Id).ToArray();
        var certificates = await db.CalibrationCertificates.AsNoTracking().Where(x =>
                ids.Contains(x.MachineId) && x.CalibrationDate <= asOf && x.Result != CalibrationResult.FAIL)
            .Select(x => new { x.Id, x.MachineId, x.CalibrationDate, x.ExpiryDate, x.CreatedAt })
            .ToListAsync(ct);
        var current = certificates.GroupBy(x => x.MachineId).ToDictionary(x => x.Key,
            x => x.OrderByDescending(y => y.CalibrationDate).ThenByDescending(y => y.CreatedAt)
                .ThenByDescending(y => y.Id).First());
        var statuses = machines.Select(machine =>
        {
            current.TryGetValue(machine.Id, out var certificate);
            if (!machine.CalibrationRequired) return CalibrationValidityStatus.NOT_REQUIRED;
            if (certificate is null || certificate.ExpiryDate <= asOf) return CalibrationValidityStatus.EXPIRED;
            return certificate.ExpiryDate.DayNumber - asOf.DayNumber <= ReportingDefinitions.ExpiringCalibrationDays
                ? CalibrationValidityStatus.EXPIRING_SOON : CalibrationValidityStatus.VALID;
        }).ToArray();
        return Enum.GetValues<CalibrationValidityStatus>()
            .Select(status => new CalibrationStatusDistributionDto(status, statuses.Count(x => x == status)))
            .ToArray();
    }

    private static IQueryable<Domain.Entities.WorkOrder> FilterWorkOrders(
        IQueryable<Domain.Entities.WorkOrder> query, Guid? departmentId, Guid? locationId)
    {
        if (departmentId.HasValue) query = query.Where(x => x.Machine.DepartmentId == departmentId);
        if (locationId.HasValue) query = query.Where(x => x.Machine.LocationId == locationId);
        return query;
    }

    private static void ValidateMonth(int year, int month)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            throw new AppException(400, "Year must be between 2000 and 2100 and Month between 1 and 12.");
    }

    private sealed record TrendWorkOrder(DateOnly DueDate, WorkOrderLifecycleStatus Status,
        DateTime? SubmittedAt, DateTime? ApprovedAt);
    private sealed record TrendBreakdown(DateTime ReportedAt, bool MachineStopped,
        DateTime? ReturnedToServiceAt);
    private sealed record MonthlyBreakdown(DateTime ReportedAt, BreakdownSeverity Severity,
        bool MachineStopped, DateTime? ReturnedToServiceAt);
}
