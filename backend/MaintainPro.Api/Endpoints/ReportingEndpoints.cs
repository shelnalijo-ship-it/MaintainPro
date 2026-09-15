using MaintainPro.Api.Security;
using MaintainPro.Application.Reporting;
using Microsoft.AspNetCore.Mvc;

namespace MaintainPro.Api.Endpoints;

public static class ReportingEndpoints
{
    public static void MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var dashboard = app.MapGroup("/api/v1/dashboard").WithTags("Dashboards");
        dashboard.MapGet("/manager", async ([AsParameters] DashboardQuery query,
            DashboardService service, CancellationToken ct) => TypedResults.Ok(await service.ManagerAsync(query, ct)))
            .RequireAuthorization(Policies.ViewManagerDashboard);
        dashboard.MapGet("/supervisor", async ([AsParameters] DashboardQuery query,
            DashboardService service, CancellationToken ct) => TypedResults.Ok(await service.SupervisorAsync(query, ct)))
            .RequireAuthorization(Policies.ViewSupervisorDashboard);
        dashboard.MapGet("/technician", async ([AsParameters] DashboardQuery query,
            DashboardService service, CancellationToken ct) => TypedResults.Ok(await service.TechnicianAsync(query, ct)))
            .RequireAuthorization(Policies.ViewTechnicianDashboard);

        var reports = app.MapGroup("/api/v1/reports").WithTags("Management reports")
            .RequireAuthorization(Policies.ViewReports);
        reports.MapGet("/preventive-maintenance", async ([AsParameters] PreventiveMaintenanceReportQuery query,
            ReportQueryService service, CancellationToken ct) => TypedResults.Ok(await service.PreventiveAsync(query, ct)));
        reports.MapGet("/overdue-maintenance", async ([AsParameters] OverdueMaintenanceReportQuery query,
            ReportQueryService service, CancellationToken ct) => TypedResults.Ok(await service.OverdueAsync(query, ct)));
        reports.MapGet("/breakdowns", async ([AsParameters] BreakdownReportQuery query,
            ReportQueryService service, CancellationToken ct) => TypedResults.Ok(await service.BreakdownsAsync(query, ct)));
        reports.MapGet("/calibration", async ([AsParameters] CalibrationReportQuery query,
            ReportQueryService service, CancellationToken ct) => TypedResults.Ok(await service.CalibrationAsync(query, ct)));
        reports.MapGet("/technician-workload", async ([AsParameters] TechnicianWorkloadReportQuery query,
            ReportQueryService service, CancellationToken ct) => TypedResults.Ok(await service.TechnicianWorkloadAsync(query, ct)));
        reports.MapGet("/external-services", async ([AsParameters] ExternalServiceReportQuery query,
            ReportQueryService service, CancellationToken ct) => TypedResults.Ok(await service.ExternalServicesAsync(query, ct)));
        reports.MapGet("/monthly-summary", async ([AsParameters] MonthlySummaryQuery query,
            SummaryReportingService service, CancellationToken ct) => TypedResults.Ok(await service.MonthlyAsync(query, ct)));
        reports.MapGet("/trends", async ([AsParameters] TrendQuery query,
            SummaryReportingService service, CancellationToken ct) => TypedResults.Ok(await service.TrendsAsync(query, ct)));
        reports.MapGet("/export-history", async ([AsParameters] ReportExportHistoryQuery query,
            ReportExportHistoryService service, CancellationToken ct) => TypedResults.Ok(await service.ListAsync(query, ct)));

        reports.MapGet("/preventive-maintenance/export/pdf", async ([AsParameters] PreventiveMaintenanceReportQuery query,
            ReportExportCoordinator service, CancellationToken ct) => File(await service.PreventiveAsync(query, ReportExportFormat.Pdf, ct)));
        reports.MapGet("/preventive-maintenance/export/excel", async ([AsParameters] PreventiveMaintenanceReportQuery query,
            ReportExportCoordinator service, CancellationToken ct) => File(await service.PreventiveAsync(query, ReportExportFormat.Excel, ct)));
        reports.MapGet("/overdue-maintenance/export/pdf", async ([AsParameters] OverdueMaintenanceReportQuery query,
            ReportExportCoordinator service, CancellationToken ct) => File(await service.OverdueAsync(query, ReportExportFormat.Pdf, ct)));
        reports.MapGet("/overdue-maintenance/export/excel", async ([AsParameters] OverdueMaintenanceReportQuery query,
            ReportExportCoordinator service, CancellationToken ct) => File(await service.OverdueAsync(query, ReportExportFormat.Excel, ct)));
        reports.MapGet("/breakdowns/export/pdf", async ([AsParameters] BreakdownReportQuery query,
            ReportExportCoordinator service, CancellationToken ct) => File(await service.BreakdownsAsync(query, ReportExportFormat.Pdf, ct)));
        reports.MapGet("/breakdowns/export/excel", async ([AsParameters] BreakdownReportQuery query,
            ReportExportCoordinator service, CancellationToken ct) => File(await service.BreakdownsAsync(query, ReportExportFormat.Excel, ct)));
        reports.MapGet("/calibration/export/pdf", async ([AsParameters] CalibrationReportQuery query,
            ReportExportCoordinator service, CancellationToken ct) => File(await service.CalibrationAsync(query, ReportExportFormat.Pdf, ct)));
        reports.MapGet("/calibration/export/excel", async ([AsParameters] CalibrationReportQuery query,
            ReportExportCoordinator service, CancellationToken ct) => File(await service.CalibrationAsync(query, ReportExportFormat.Excel, ct)));
        reports.MapGet("/technician-workload/export/pdf", async ([AsParameters] TechnicianWorkloadReportQuery query,
            ReportExportCoordinator service, CancellationToken ct) => File(await service.TechnicianWorkloadAsync(query, ReportExportFormat.Pdf, ct)));
        reports.MapGet("/technician-workload/export/excel", async ([AsParameters] TechnicianWorkloadReportQuery query,
            ReportExportCoordinator service, CancellationToken ct) => File(await service.TechnicianWorkloadAsync(query, ReportExportFormat.Excel, ct)));
        reports.MapGet("/external-services/export/pdf", async ([AsParameters] ExternalServiceReportQuery query,
            ReportExportCoordinator service, CancellationToken ct) => File(await service.ExternalServicesAsync(query, ReportExportFormat.Pdf, ct)));
        reports.MapGet("/external-services/export/excel", async ([AsParameters] ExternalServiceReportQuery query,
            ReportExportCoordinator service, CancellationToken ct) => File(await service.ExternalServicesAsync(query, ReportExportFormat.Excel, ct)));
        reports.MapGet("/monthly-summary/export/pdf", async ([AsParameters] MonthlySummaryQuery query,
            ReportExportCoordinator service, CancellationToken ct) => File(await service.MonthlySummaryAsync(query, ReportExportFormat.Pdf, ct)));
        reports.MapGet("/monthly-summary/export/excel", async ([AsParameters] MonthlySummaryQuery query,
            ReportExportCoordinator service, CancellationToken ct) => File(await service.MonthlySummaryAsync(query, ReportExportFormat.Excel, ct)));

        app.MapGet("/api/v1/reports/machine-history/{machineId:guid}", async (Guid machineId,
            [AsParameters] MachineHistoryReportQuery query, MachineHistoryReportService service,
            CancellationToken ct) => TypedResults.Ok(await service.GetAsync(machineId, query, ct)))
            .WithTags("Management reports").RequireAuthorization(Policies.ViewMachineHistoryReports);
        app.MapGet("/api/v1/reports/machine-history/{machineId:guid}/export/pdf", async (Guid machineId,
            [AsParameters] MachineHistoryReportQuery query, ReportExportCoordinator service,
            CancellationToken ct) => File(await service.MachineHistoryAsync(machineId, query, ReportExportFormat.Pdf, ct)))
            .WithTags("Management reports").RequireAuthorization(Policies.ViewMachineHistoryReports);
        app.MapGet("/api/v1/reports/machine-history/{machineId:guid}/export/excel", async (Guid machineId,
            [AsParameters] MachineHistoryReportQuery query, ReportExportCoordinator service,
            CancellationToken ct) => File(await service.MachineHistoryAsync(machineId, query, ReportExportFormat.Excel, ct)))
            .WithTags("Management reports").RequireAuthorization(Policies.ViewMachineHistoryReports);
    }

    private static IResult File(ReportExportFile file) => Results.File(file.Content,
        file.ContentType, file.FileName);
}
