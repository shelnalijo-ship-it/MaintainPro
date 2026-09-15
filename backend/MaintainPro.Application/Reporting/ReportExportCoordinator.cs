using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Reporting;

public sealed class ReportExportCoordinator(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock, IReportExportService exporter,
    ReportQueryService reports, SummaryReportingService summaries,
    MachineHistoryReportService history)
{
    public async Task<ReportExportFile> PreventiveAsync(PreventiveMaintenanceReportQuery query,
        ReportExportFormat format, CancellationToken ct = default)
    {
        var data = await reports.PreventiveAsync(query, true, ct);
        return await ExportAsync(new("preventive-maintenance", "Preventive Maintenance Report",
            "preventive-maintenance", Filters(("From", query.From), ("To", query.To),
                ("Machine", query.MachineId), ("Technician", query.TechnicianId),
                ("Supervisor", query.SupervisorId), ("Status", query.Status), ("Priority", query.Priority)),
            [C("Work Order"), C("Machine Code"), C("Machine Name"), C("Plan"), C("Planned", ReportCellKind.Date),
                C("Due", ReportCellKind.Date), C("Started", ReportCellKind.DateTime), C("Completed", ReportCellKind.DateTime),
                C("Submitted", ReportCellKind.DateTime), C("Approved", ReportCellKind.DateTime), C("Technician"),
                C("Supervisor"), C("Status"), C("Days Overdue", ReportCellKind.Integer),
                C("Escalation Level", ReportCellKind.Integer)],
            data.Items.Select(x => Row(x.WorkOrderNumber, x.MachineCode, x.MachineName, x.PlanName,
                x.PlannedDate, x.DueDate, x.StartedAt, x.CompletedAt, x.SubmittedAt, x.ApprovedAt,
                x.Technician, x.Supervisor, x.LifecycleStatus, x.DaysOverdue, x.EscalationLevel)).ToArray()), format, ct);
    }

    public async Task<ReportExportFile> OverdueAsync(OverdueMaintenanceReportQuery query,
        ReportExportFormat format, CancellationToken ct = default)
    {
        var data = await reports.OverdueAsync(query, true, ct);
        return await ExportAsync(new("overdue-maintenance", "Overdue Maintenance Report",
            "overdue-maintenance", Filters(("AsOf", query.AsOf), ("DueFrom", query.DueFrom),
                ("Machine", query.MachineId), ("Technician", query.TechnicianId),
                ("Supervisor", query.SupervisorId), ("EscalationLevel", query.EscalationLevel)),
            [C("Work Order"), C("Machine Code"), C("Machine Name"), C("Technician"), C("Supervisor"),
                C("Due", ReportCellKind.Date), C("Lifecycle"), C("Days Overdue", ReportCellKind.Integer),
                C("Escalation Level", ReportCellKind.Integer), C("Last Escalation", ReportCellKind.DateTime),
                C("Submission State")],
            data.Items.Select(x => Row(x.WorkOrderNumber, x.MachineCode, x.MachineName,
                x.Technician, x.Supervisor, x.DueDate, x.LifecycleStatus, x.DaysOverdue,
                x.EscalationLevel, x.LastEscalationDate, x.SubmissionState)).ToArray()), format, ct);
    }

    public async Task<ReportExportFile> BreakdownsAsync(BreakdownReportQuery query,
        ReportExportFormat format, CancellationToken ct = default)
    {
        var data = await reports.BreakdownsAsync(query, true, ct);
        return await ExportAsync(new("breakdowns", "Breakdown Report", "breakdowns",
            Filters(("From", query.From), ("To", query.To), ("Machine", query.MachineId),
                ("Severity", query.Severity), ("Technician", query.TechnicianId),
                ("Supervisor", query.SupervisorId), ("Status", query.Status),
                ("MachineStopped", query.MachineStopped)),
            [C("Breakdown"), C("Machine Code"), C("Machine Name"), C("Reported", ReportCellKind.DateTime),
                C("Severity"), C("Stopped", ReportCellKind.Boolean), C("Downtime Minutes", ReportCellKind.Decimal),
                C("Root Cause"), C("Corrective Action"), C("Technician"), C("Supervisor"),
                C("Final Status"), C("Returned To Service", ReportCellKind.DateTime)],
            data.Items.Select(x => Row(x.BreakdownNumber, x.MachineCode, x.MachineName,
                x.ReportedAt, x.Severity, x.MachineStopped, x.DowntimeMinutes, x.RootCause,
                x.CorrectiveAction, x.Technician, x.Supervisor, x.FinalStatus, x.ReturnedToServiceAt)).ToArray()), format, ct);
    }

    public async Task<ReportExportFile> CalibrationAsync(CalibrationReportQuery query,
        ReportExportFormat format, CancellationToken ct = default)
    {
        var data = await reports.CalibrationAsync(query, true, ct);
        return await ExportAsync(new("calibration", "Calibration Report", "calibration",
            Filters(("Machine", query.MachineId), ("Provider", query.Provider),
                ("ValidityStatus", query.ValidityStatus), ("RenewalStatus", query.RenewalStatus),
                ("ExpiryFrom", query.ExpiryFrom), ("ExpiryTo", query.ExpiryTo)),
            [C("Machine Code"), C("Machine Name"), C("Certificate"), C("Provider"),
                C("Calibration Date", ReportCellKind.Date), C("Expiry Date", ReportCellKind.Date),
                C("Days Remaining", ReportCellKind.Integer), C("Result"), C("Validity"), C("Renewal")],
            data.Items.Select(x => Row(x.MachineCode, x.MachineName, x.CertificateNumber,
                x.Provider, x.CalibrationDate, x.ExpiryDate, x.DaysRemaining, x.Result,
                x.ValidityStatus, x.RenewalStatus)).ToArray()), format, ct);
    }

    public async Task<ReportExportFile> TechnicianWorkloadAsync(TechnicianWorkloadReportQuery query,
        ReportExportFormat format, CancellationToken ct = default)
    {
        var data = await reports.TechnicianWorkloadAsync(query, true, ct);
        return await ExportAsync(new("technician-workload", "Technician Workload Report",
            "technician-workload", Filters(("From", query.From), ("To", query.To),
                ("Technician", query.TechnicianId), ("Department", query.DepartmentId),
                ("Location", query.LocationId)),
            [C("Employee ID"), C("Technician"), C("Assigned", ReportCellKind.Integer),
                C("Started", ReportCellKind.Integer), C("Approved", ReportCellKind.Integer),
                C("Rejected", ReportCellKind.Integer), C("Awaiting Approval", ReportCellKind.Integer),
                C("Overdue", ReportCellKind.Integer), C("Avg Execution Minutes", ReportCellKind.Decimal),
                C("Breakdown Assignments", ReportCellKind.Integer), C("Corrective Closures", ReportCellKind.Integer)],
            data.Items.Select(x => Row(x.EmployeeId, x.TechnicianName, x.Assigned, x.Started,
                x.Approved, x.Rejected, x.AwaitingApproval, x.Overdue,
                x.AverageExecutionMinutes, x.BreakdownAssignments, x.CorrectiveClosures)).ToArray()), format, ct);
    }

    public async Task<ReportExportFile> ExternalServicesAsync(ExternalServiceReportQuery query,
        ReportExportFormat format, CancellationToken ct = default)
    {
        var data = await reports.ExternalServicesAsync(query, true, ct);
        return await ExportAsync(new("external-services", "External Service Report",
            "external-services", Filters(("From", query.From), ("To", query.To),
                ("Machine", query.MachineId), ("Company", query.Company),
                ("ServiceType", query.ServiceType), ("FollowUpStatus", query.FollowUpStatus)),
            [C("Service"), C("Machine Code"), C("Machine Name"), C("Company"), C("Technician"),
                C("Service Date", ReportCellKind.Date), C("Type"), C("Findings"),
                C("Cost", ReportCellKind.Decimal), C("PO"), C("Invoice"),
                C("Follow Up", ReportCellKind.Date), C("Next Service", ReportCellKind.Date),
                C("Follow-up Status"), C("Attachments", ReportCellKind.Integer)],
            data.Items.Select(x => Row(x.ServiceNumber, x.MachineCode, x.MachineName,
                x.Company, x.Technician, x.ServiceDate, x.ServiceType, x.Findings, x.Cost,
                x.PurchaseOrderNumber, x.InvoiceNumber, x.FollowUpDate, x.NextServiceDate,
                x.FollowUpStatus, x.AttachmentCount)).ToArray()), format, ct);
    }

    public async Task<ReportExportFile> MonthlySummaryAsync(MonthlySummaryQuery query,
        ReportExportFormat format, CancellationToken ct = default)
    {
        var data = await summaries.MonthlyAsync(query, ct);
        var rows = new[] { data.Current, data.Previous }.Select(x => Row(x.Year, x.Month,
            x.PlannedMaintenance, x.ApprovedMaintenance, x.OverdueMaintenance,
            x.Compliance.Percentage, x.BreakdownCount, x.CriticalBreakdownCount,
            x.TotalDowntimeMinutes, x.ExternalServicesCount, x.CalibrationExpired,
            x.CalibrationExpiring, x.Escalations)).ToArray();
        return await ExportAsync(new("monthly-summary", "Monthly Maintenance Summary",
            $"monthly-summary-{query.Year:D4}-{query.Month:D2}",
            Filters(("Year", query.Year), ("Month", query.Month),
                ("Department", query.DepartmentId), ("Location", query.LocationId)),
            [C("Year", ReportCellKind.Integer), C("Month", ReportCellKind.Integer),
                C("Planned", ReportCellKind.Integer), C("Approved", ReportCellKind.Integer),
                C("Overdue", ReportCellKind.Integer), C("Compliance", ReportCellKind.Percentage),
                C("Breakdowns", ReportCellKind.Integer), C("Critical Breakdowns", ReportCellKind.Integer),
                C("Downtime Minutes", ReportCellKind.Decimal), C("External Services", ReportCellKind.Integer),
                C("Calibration Expired", ReportCellKind.Integer), C("Calibration Expiring", ReportCellKind.Integer),
                C("Escalations", ReportCellKind.Integer)], rows), format, ct);
    }

    public async Task<ReportExportFile> MachineHistoryAsync(Guid machineId,
        MachineHistoryReportQuery query, ReportExportFormat format, CancellationToken ct = default)
    {
        var data = await history.ExportAsync(machineId, query, ct);
        return await ExportAsync(new("machine-history", "Machine History Report - " + data.MachineCode,
            "machine-history-" + SafeToken(data.MachineCode),
            Filters(("From", query.From), ("To", query.To), ("Module", query.Module)),
            [C("Module"), C("Event"), C("Occurred", ReportCellKind.DateTime), C("Reference"),
                C("Summary"), C("Details")],
            data.Events.Items.Select(x => Row(x.Module, x.EventType, x.OccurredAt,
                x.Reference, x.Summary, x.Details)).ToArray()), format, ct);
    }

    private async Task<ReportExportFile> ExportAsync(ReportTable table, ReportExportFormat format,
        CancellationToken ct)
    {
        var userId = Guard.Authenticated(currentUser);
        var generatedBy = await db.Users.AsNoTracking().Where(x => x.Id == userId)
            .Select(x => x.EmployeeId + " - " + x.FirstName + " " + x.LastName)
            .SingleOrDefaultAsync(ct) ?? userId.ToString();
        var now = clock.GetUtcNow().UtcDateTime;
        var file = exporter.Export(new(table, now, generatedBy), format);
        audit.Record("Report.Exported", "Report", null, newValues: new
        {
            table.ReportName, Format = format.ToString().ToUpperInvariant(),
            file.RowCount, file.FileName, GeneratedAt = now, GeneratedByUserId = userId,
            table.FilterSummary
        });
        await db.SaveChangesAsync(ct);
        return file;
    }

    private static ReportColumn C(string header, ReportCellKind kind = ReportCellKind.Text) => new(header, kind);
    private static IReadOnlyList<object?> Row(params object?[] values) => values;
    private static string Filters(params (string Name, object? Value)[] values)
    {
        var selected = values.Where(x => x.Value is not null).Select(x => x.Name + "=" + x.Value).ToArray();
        return selected.Length == 0 ? "No optional filters" : string.Join("; ", selected);
    }
    private static string SafeToken(string value)
    {
        var token = new string(value.Where(x => char.IsLetterOrDigit(x) || x is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(token) ? "machine" : token.ToLowerInvariant();
    }
}
