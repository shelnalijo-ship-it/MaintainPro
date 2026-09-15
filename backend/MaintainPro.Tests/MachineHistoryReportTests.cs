using MaintainPro.Application.Reporting;

namespace MaintainPro.Tests;

public sealed class MachineHistoryReportTests
{
    [Fact]
    public async Task Machine_history_aggregates_modules_in_reverse_chronological_order()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);

        var report = await fixture.MachineHistoryReports.GetAsync(data.Machine.Id,
            new(new(2026, 8, 1), new(2026, 9, 30), PageSize: 100));

        Assert.Contains(report.Events.Items, x => x.Module == "PREVENTIVE_MAINTENANCE" && x.EventType == "WorkOrder.Approved");
        Assert.Contains(report.Events.Items, x => x.Module == "BREAKDOWN");
        Assert.Contains(report.Events.Items, x => x.Module == "CALIBRATION");
        Assert.Contains(report.Events.Items, x => x.Module == "EXTERNAL_SERVICE");
        Assert.Contains(report.Events.Items, x => x.Module == "MACHINE_DOCUMENT");
        Assert.Contains(report.Events.Items, x => x.Module == "ASSIGNMENT");
        Assert.Contains(report.Events.Items, x => x.Module == "MACHINE_STATUS");
        Assert.Equal(report.Events.Items.OrderByDescending(x => x.OccurredAt), report.Events.Items);
    }

    [Fact]
    public async Task Machine_history_supports_module_filter_and_technician_scope()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);

        var report = await fixture.MachineHistoryReports.GetAsync(data.Machine.Id,
            new(Module: "breakdown"));
        Assert.NotEmpty(report.Events.Items);
        Assert.All(report.Events.Items, x => Assert.Equal("BREAKDOWN", x.Module));
        await ModuleFixture.ExpectStatusAsync(404, () =>
            fixture.MachineHistoryReports.GetAsync(data.OtherMachine.Id, new()));
    }
}

