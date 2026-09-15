using MaintainPro.Application.Reporting;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Tests;

public sealed class ReportQueryTests
{
    [Fact]
    public async Task Preventive_report_applies_machine_department_technician_supervisor_and_status_filters()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);

        var result = await fixture.ReportQueries.PreventiveAsync(new(new(2026, 9, 1), new(2026, 9, 30),
            MachineId: data.Machine.Id, DepartmentId: data.Department.Id,
            LocationId: data.Location.Id, TechnicianId: data.Technician.Id,
            SupervisorId: data.Supervisor.Id, Status: WorkOrderLifecycleStatus.APPROVED,
            Priority: MaintenancePriority.HIGH));

        var row = Assert.Single(result.Items);
        Assert.Equal(data.Approved.Id, row.WorkOrderId);
        Assert.Equal("Monthly inspection", row.PlanName);
        Assert.Equal(0, row.DaysOverdue);
    }

    [Fact]
    public async Task Overdue_report_uses_execution_state_and_returns_escalation_metadata()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);

        var result = await fixture.ReportQueries.OverdueAsync(new(AsOf: new(2026, 9, 15),
            MachineId: data.Machine.Id, TechnicianId: data.Technician.Id,
            SupervisorId: data.Supervisor.Id, EscalationLevel: 2,
            DepartmentId: data.Department.Id, LocationId: data.Location.Id));

        var row = Assert.Single(result.Items);
        Assert.Equal(data.Overdue.Id, row.WorkOrderId);
        Assert.Equal(5, row.DaysOverdue);
        Assert.Equal(2, row.EscalationLevel);
        Assert.NotNull(row.LastEscalationDate);
        Assert.Equal("NOT_SUBMITTED", row.SubmissionState);
    }

    [Fact]
    public async Task Breakdown_report_calculates_downtime_and_uses_approved_corrective_facts()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);

        var result = await fixture.ReportQueries.BreakdownsAsync(new(new(2026, 9, 1), new(2026, 9, 30),
            MachineId: data.Machine.Id, Status: BreakdownStatus.CLOSED,
            DepartmentId: data.Department.Id, LocationId: data.Location.Id));

        var row = Assert.Single(result.Items);
        Assert.Equal("Worn bearing", row.RootCause);
        Assert.Equal("Bearing replaced", row.CorrectiveAction);
        Assert.Equal(1560m, row.DowntimeMinutes);
        Assert.NotNull(row.ReturnedToServiceAt);
    }

    [Fact]
    public async Task Calibration_report_returns_current_validity_expiry_and_renewal_state()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);

        var result = await fixture.ReportQueries.CalibrationAsync(new(MachineId: data.Machine.Id,
            Provider: "precision", ValidityStatus: CalibrationValidityStatus.EXPIRING_SOON,
            RenewalStatus: CalibrationRenewalStatus.IN_PROGRESS,
            ExpiryFrom: new(2026, 10, 1), ExpiryTo: new(2026, 10, 31)));

        var row = Assert.Single(result.Items);
        Assert.Equal("CAL-1001", row.CertificateNumber);
        Assert.Equal(20, row.DaysRemaining);
        Assert.Equal(CalibrationResult.PASS, row.Result);
    }

    [Fact]
    public async Task Workload_report_returns_operational_counts_and_execution_average()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);

        var result = await fixture.ReportQueries.TechnicianWorkloadAsync(new(new(2026, 9, 1),
            new(2026, 9, 30), data.Technician.Id, data.Department.Id, data.Location.Id));

        var row = Assert.Single(result.Items);
        Assert.Equal(6, row.Assigned);
        Assert.Equal(3, row.Started);
        Assert.Equal(1, row.Approved);
        Assert.Equal(1, row.Rejected);
        Assert.Equal(1, row.AwaitingApproval);
        Assert.Equal(1, row.Overdue);
        Assert.Equal(51.67m, row.AverageExecutionMinutes);
        Assert.Equal(2, row.BreakdownAssignments);
        Assert.Equal(1, row.CorrectiveClosures);
    }

    [Fact]
    public async Task External_service_report_filters_followups_and_redacts_cost_for_supervisors()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);
        fixture.ActAs(data.Supervisor);

        var result = await fixture.ReportQueries.ExternalServicesAsync(new(new(2026, 9, 1),
            new(2026, 9, 30), data.Machine.Id, "vendor", ExternalServiceType.INSPECTION,
            ExternalServiceFollowUpStatus.OVERDUE, data.Department.Id, data.Location.Id));

        var row = Assert.Single(result.Items);
        Assert.Null(row.Cost);
        Assert.Equal(1, row.AttachmentCount);
        Assert.Equal(ExternalServiceFollowUpStatus.OVERDUE, row.FollowUpStatus);
        fixture.ActAs(data.Manager);
        var management = await fixture.ReportQueries.ExternalServicesAsync(new(new(2026, 9, 1),
            new(2026, 9, 30), data.Machine.Id));
        Assert.Equal(500m, Assert.Single(management.Items).Cost);
    }

    [Fact]
    public async Task Supervisor_report_scope_excludes_other_supervisors_records_before_paging()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);
        fixture.ActAs(data.Supervisor);

        var work = await fixture.ReportQueries.PreventiveAsync(new(new(2026, 9, 1), new(2026, 9, 30),
            Page: 1, PageSize: 100));
        var breakdowns = await fixture.ReportQueries.BreakdownsAsync(new(new(2026, 9, 1), new(2026, 9, 30),
            Page: 1, PageSize: 100));

        Assert.Equal(7, work.TotalCount);
        Assert.All(work.Items, x => Assert.Equal(data.Machine.Id, x.MachineId));
        Assert.Equal(2, breakdowns.TotalCount);
        Assert.All(breakdowns.Items, x => Assert.Equal(data.Machine.Id, x.MachineId));
    }

    [Fact]
    public async Task Technician_cannot_open_management_reports()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ReportingTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);

        await ModuleFixture.ExpectStatusAsync(403, () =>
            fixture.ReportQueries.PreventiveAsync(new(new(2026, 9, 1), new(2026, 9, 30))));
    }
}

