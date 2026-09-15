using MaintainPro.Application.Reporting;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Tests;

public sealed class ReportingDefinitionTests
{
    [Fact]
    public void Compliance_includes_late_approvals_excludes_cancelled_and_exposes_denominator()
    {
        var asOf = new DateOnly(2026, 9, 15);
        var result = ReportingDefinitions.Compliance([
            new(true, WorkOrderLifecycleStatus.APPROVED, new DateTime(2026, 9, 12), new(2026, 9, 10)),
            new(true, WorkOrderLifecycleStatus.ASSIGNED, null, new(2026, 9, 11)),
            new(true, WorkOrderLifecycleStatus.ASSIGNED, null, new(2026, 9, 20)),
            new(true, WorkOrderLifecycleStatus.CANCELLED, null, new(2026, 9, 1)),
            new(false, WorkOrderLifecycleStatus.APPROVED, null, new(2026, 9, 2))
        ], asOf);

        Assert.Equal(33.33m, result.Percentage);
        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(1, result.OverdueCount);
        Assert.Equal(2, result.PendingCount);
        Assert.Equal(3, result.TotalDueCount);
    }

    [Fact]
    public void Compliance_returns_null_percentage_for_zero_denominator()
    {
        var result = ReportingDefinitions.Compliance([], new(2026, 9, 15));
        Assert.Null(result.Percentage);
        Assert.Equal(0, result.TotalDueCount);
    }

    [Fact]
    public void UTC_date_boundaries_and_follow_up_states_are_explicit()
    {
        var today = new DateOnly(2026, 9, 15);
        Assert.Equal(new DateOnly(2026, 9, 20), ReportingDefinitions.EndOfIsoWeek(today));
        Assert.Equal(ExternalServiceFollowUpStatus.OVERDUE,
            ReportingDefinitions.FollowUpStatus(today.AddDays(-1), today.AddDays(10), today));
        Assert.Equal(ExternalServiceFollowUpStatus.DUE,
            ReportingDefinitions.FollowUpStatus(today, null, today));
        Assert.Equal(ExternalServiceFollowUpStatus.UPCOMING,
            ReportingDefinitions.FollowUpStatus(null, today.AddDays(1), today));
        Assert.Equal(ExternalServiceFollowUpStatus.NONE,
            ReportingDefinitions.FollowUpStatus(null, null, today));
    }
}

