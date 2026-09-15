using MaintainPro.Application.Breakdowns;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Tests;

public sealed class BreakdownTimingTests
{
    private static readonly DateTime ReportedAt = new(2026, 9, 15, 23, 30, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(60, 1)]
    [InlineData(75, 1.25)]
    [InlineData(344, 5.73)]
    [InlineData(5400, 90)]
    [InlineData(-60, 0)]
    public void Stopped_downtime_uses_elapsed_UTC_minutes_and_two_decimal_rounding(int seconds, decimal expected)
    {
        var breakdown = Create();
        Assert.Equal(expected, BreakdownTiming.DowntimeMinutes(breakdown, ReportedAt.AddSeconds(seconds)));
    }

    [Theory]
    [InlineData(BreakdownStatus.REPORTED)]
    [InlineData(BreakdownStatus.IN_PROGRESS)]
    [InlineData(BreakdownStatus.AWAITING_APPROVAL)]
    [InlineData(BreakdownStatus.CLOSED)]
    public void Stopped_time_continues_through_review_and_closure_until_explicit_return(BreakdownStatus status)
    {
        var breakdown = Create();
        breakdown.Status = status;
        breakdown.CompletedAt = ReportedAt.AddMinutes(10);
        breakdown.SubmittedAt = ReportedAt.AddMinutes(11);
        if (status == BreakdownStatus.CLOSED) breakdown.ClosedAt = ReportedAt.AddMinutes(20);
        Assert.Equal(120m, BreakdownTiming.DowntimeMinutes(breakdown, ReportedAt.AddHours(2)));
    }

    [Fact]
    public void Return_to_service_freezes_downtime_and_non_stopped_reports_always_have_zero()
    {
        var breakdown = Create();
        breakdown.ReturnedToServiceAt = ReportedAt.AddMinutes(45);
        Assert.Equal(45m, BreakdownTiming.DowntimeMinutes(breakdown, ReportedAt.AddDays(5)));
        breakdown.MachineStopped = false;
        Assert.Equal(0m, BreakdownTiming.DowntimeMinutes(breakdown, ReportedAt.AddDays(5)));
    }

    private static Breakdown Create() => new()
    {
        BreakdownNumber = "BD-2026-0001", MachineCode = "PUMP-1", MachineName = "Pump",
        ReporterEmployeeId = "EMP-1", ReporterName = "Reporter", SupervisorEmployeeId = "EMP-2",
        SupervisorName = "Supervisor", Description = "Bearing seized", ReportedAt = ReportedAt,
        MachineStopped = true, Severity = BreakdownSeverity.HIGH
    };
}
