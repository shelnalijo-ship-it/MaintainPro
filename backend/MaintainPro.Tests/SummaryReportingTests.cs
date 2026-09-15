using MaintainPro.Application.Reporting;

namespace MaintainPro.Tests;

public sealed class SummaryReportingTests
{
    [Fact]
    public async Task Monthly_summary_returns_actual_current_and_previous_month_values()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await ReportingTestData.CreateAsync(fixture);

        var result = await fixture.SummaryReports.MonthlyAsync(new(2026, 9));

        Assert.Equal(7, result.Current.PlannedMaintenance);
        Assert.Equal(1, result.Current.ApprovedMaintenance);
        Assert.Equal(1, result.Current.OverdueMaintenance);
        Assert.Equal(3, result.Current.BreakdownCount);
        Assert.Equal(1, result.Current.CriticalBreakdownCount);
        Assert.Equal(1, result.Current.ExternalServicesCount);
        Assert.Equal(1, result.Current.CalibrationExpired);
        Assert.Equal(1, result.Current.CalibrationExpiring);
        Assert.Equal(1, result.Current.Escalations);
        Assert.Equal(8, result.Previous.Month);
        Assert.Equal(0, result.Previous.PlannedMaintenance);
        Assert.Null(result.Previous.Compliance.Percentage);
    }

    [Fact]
    public async Task Trends_return_zero_filled_calendar_buckets_and_current_calibration_distribution()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await ReportingTestData.CreateAsync(fixture);

        var result = await fixture.SummaryReports.TrendsAsync(new(new(2026, 8, 1), new(2026, 9, 30)));

        Assert.Equal(2, result.Maintenance.Count);
        Assert.Equal(0, result.Maintenance[0].Approved);
        Assert.Equal(1, result.Maintenance[1].Approved);
        Assert.Equal(3, result.Breakdowns[1].BreakdownCount);
        Assert.Equal(7, result.TechnicianWorkload[1].Assigned);
        Assert.Equal(1, result.CalibrationStatus.Single(x =>
            x.Status == MaintainPro.Domain.Enums.CalibrationValidityStatus.EXPIRED).Count);
    }
}
