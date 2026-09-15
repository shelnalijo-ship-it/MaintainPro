using MaintainPro.Application.Reporting;

namespace MaintainPro.Tests;

public sealed class DashboardTests
{
    [Fact]
    public async Task Manager_dashboard_returns_central_KPIs_and_period_compliance()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);

        var dashboard = await fixture.Dashboards.ManagerAsync(new(new(2026, 9, 1), new(2026, 9, 30)));

        Assert.Equal(2, dashboard.Machines.TotalMachines);
        Assert.Equal(1, dashboard.Machines.OperationalMachines);
        Assert.Equal(1, dashboard.Machines.MachinesUnderMaintenance);
        Assert.Equal(2, dashboard.Maintenance.MaintenanceDueToday);
        Assert.Equal(3, dashboard.Maintenance.MaintenanceDueThisWeek);
        Assert.Equal(1, dashboard.Maintenance.MaintenanceOverdue);
        Assert.Equal(1, dashboard.Maintenance.MaintenanceAwaitingApproval);
        Assert.Equal(1, dashboard.Maintenance.MaintenanceEscalated);
        Assert.Equal(2, dashboard.Breakdowns.OpenBreakdowns);
        Assert.Equal(1, dashboard.Breakdowns.CriticalBreakdowns);
        Assert.Equal(1, dashboard.Calibration.CalibrationExpiringWithin30Days);
        Assert.Equal(1, dashboard.Calibration.CalibrationExpired);
        Assert.Equal(1, dashboard.Calibration.CalibrationRenewalInProgress);
        Assert.Equal(1, dashboard.ExternalServiceFollowUpsDue);
        Assert.Equal(7, dashboard.MaintenanceCompliance.TotalDueCount);
        Assert.Equal(14.29m, dashboard.MaintenanceCompliance.Percentage);
        Assert.Contains(dashboard.EscalationAlerts, x => x.WorkOrderId == data.Overdue.Id);
    }

    [Fact]
    public async Task Supervisor_dashboard_is_scoped_before_aggregation()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);
        fixture.ActAs(data.Supervisor);

        var dashboard = await fixture.Dashboards.SupervisorAsync(new(new(2026, 9, 1), new(2026, 9, 30)));

        Assert.Equal("SUPERVISOR", dashboard.Scope);
        Assert.Equal(1, dashboard.Machines.TotalMachines);
        Assert.Equal(1, dashboard.Maintenance.MaintenanceDueToday);
        Assert.Equal(1, dashboard.Breakdowns.OpenBreakdowns);
        Assert.Equal(6, dashboard.MaintenanceCompliance.TotalDueCount);
        Assert.All(dashboard.TechnicianWorkload, x => Assert.Equal(data.Technician.Id, x.TechnicianId));
    }

    [Fact]
    public async Task Technician_dashboard_contains_only_the_requesting_technicians_work()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);

        var dashboard = await fixture.Dashboards.TechnicianAsync(new());

        Assert.Equal(1, dashboard.DueToday);
        Assert.Equal(1, dashboard.Upcoming);
        Assert.Equal(1, dashboard.Overdue);
        Assert.Equal(1, dashboard.Rejected);
        Assert.Equal(1, dashboard.InProgress);
        Assert.Equal(0, dashboard.NotificationsUnread);
        Assert.Equal(1, dashboard.OpenBreakdownAssignments);
        Assert.Equal(1, dashboard.MyMachinesCount);
    }

    [Fact]
    public async Task Manager_dashboard_handles_an_empty_system_without_fabricating_compliance()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();

        var dashboard = await fixture.Dashboards.ManagerAsync(new());

        Assert.Equal(0, dashboard.Machines.TotalMachines);
        Assert.Equal(0, dashboard.Maintenance.MaintenanceDueToday);
        Assert.Null(dashboard.MaintenanceCompliance.Percentage);
        Assert.Empty(dashboard.TechnicianWorkload);
    }
}
