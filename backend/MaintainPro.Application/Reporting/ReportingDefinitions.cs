using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Reporting;

/// <summary>Single source of truth for reporting date boundaries and KPI classifications.</summary>
public static class ReportingDefinitions
{
    public const int ExportRowLimit = 10_000;
    public const int TrendMonthLimit = 36;
    public const int ExpiringCalibrationDays = 60;

    public static (DateOnly From, DateOnly To) Period(DateOnly? from, DateOnly? to,
        DateOnly today, int maximumDays = 1096)
    {
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;
        if (start > end) throw new AppException(400, "From date cannot follow To date.");
        if (end.DayNumber - start.DayNumber > maximumDays)
            throw new AppException(400, $"The requested period cannot exceed {maximumDays + 1} days.");
        return (start, end);
    }

    public static (DateTime From, DateTime ToExclusive) UtcBounds(DateOnly from, DateOnly to) =>
        (from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

    public static DateOnly EndOfIsoWeek(DateOnly today)
    {
        var mondayOffset = ((int)today.DayOfWeek + 6) % 7;
        return today.AddDays(6 - mondayOffset);
    }

    public static bool RequiresExecution(WorkOrderLifecycleStatus status, DateTime? submittedAt) =>
        submittedAt is null && status is not WorkOrderLifecycleStatus.APPROVED
            and not WorkOrderLifecycleStatus.CANCELLED
            and not WorkOrderLifecycleStatus.AWAITING_APPROVAL;

    public static bool IsOverdue(WorkOrderLifecycleStatus status, DateTime? submittedAt,
        DateOnly dueDate, DateOnly asOf) => RequiresExecution(status, submittedAt) && dueDate < asOf;

    public static int DaysOverdue(WorkOrderLifecycleStatus status, DateTime? submittedAt,
        DateOnly dueDate, DateOnly asOf) => IsOverdue(status, submittedAt, dueDate, asOf)
            ? asOf.DayNumber - dueDate.DayNumber : 0;

    public static bool IsOpen(BreakdownStatus status) =>
        status is not BreakdownStatus.CLOSED and not BreakdownStatus.CANCELLED;

    public static CalibrationValidityStatus CalibrationStatus(bool required,
        CalibrationCertificate? current, DateOnly today)
    {
        if (!required) return CalibrationValidityStatus.NOT_REQUIRED;
        if (current is null) return CalibrationValidityStatus.EXPIRED;
        var days = current.ExpiryDate.DayNumber - today.DayNumber;
        return days <= 0 ? CalibrationValidityStatus.EXPIRED
            : days <= ExpiringCalibrationDays ? CalibrationValidityStatus.EXPIRING_SOON
            : CalibrationValidityStatus.VALID;
    }

    public static ExternalServiceFollowUpStatus FollowUpStatus(DateOnly? followUp,
        DateOnly? nextService, DateOnly today)
    {
        var dates = new[] { followUp, nextService }.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        if (dates.Length == 0) return ExternalServiceFollowUpStatus.NONE;
        var due = dates.Min();
        return due < today ? ExternalServiceFollowUpStatus.OVERDUE
            : due == today ? ExternalServiceFollowUpStatus.DUE
            : ExternalServiceFollowUpStatus.UPCOMING;
    }

    public static MaintenanceComplianceDto Compliance(IEnumerable<ComplianceWorkOrder> orders,
        DateOnly asOf)
    {
        var included = orders.Where(x => x.IsScheduled && x.Status != WorkOrderLifecycleStatus.CANCELLED).ToArray();
        var approved = included.Count(x => x.Status == WorkOrderLifecycleStatus.APPROVED);
        var pending = included.Length - approved;
        var overdue = included.Count(x => x.Status != WorkOrderLifecycleStatus.APPROVED &&
            IsOverdue(x.Status, x.SubmittedAt, x.DueDate, asOf));
        var percentage = included.Length == 0 ? (decimal?)null
            : decimal.Round(approved * 100m / included.Length, 2);
        return new(percentage, approved, overdue, pending, included.Length);
    }
}

public sealed record ComplianceWorkOrder(bool IsScheduled, WorkOrderLifecycleStatus Status,
    DateTime? SubmittedAt, DateOnly DueDate);

