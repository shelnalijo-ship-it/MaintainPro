using MaintainPro.Application.Common;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Reporting;

public sealed record DashboardQuery(DateOnly? From = null, DateOnly? To = null,
    Guid? DepartmentId = null, Guid? LocationId = null);

public sealed record ReportingPeriodDto(DateOnly From, DateOnly To, DateOnly AsOf,
    string DateConvention = "UTC calendar dates");

public sealed record MachineKpiDto(int TotalMachines, int OperationalMachines,
    int MachinesUnderMaintenance, int BreakdownMachines, int OutOfServiceMachines);

public sealed record MaintenanceKpiDto(int MaintenanceDueToday, int MaintenanceDueThisWeek,
    int MaintenanceOverdue, int MaintenanceAwaitingApproval, int MaintenanceEscalated);

public sealed record BreakdownKpiDto(int OpenBreakdowns, int CriticalBreakdowns);

public sealed record CalibrationKpiDto(int CalibrationValid, int CalibrationExpiringWithin60Days,
    int CalibrationExpiringWithin30Days, int CalibrationExpiringWithin7Days,
    int CalibrationExpired, int CalibrationRenewalInProgress);

public sealed record MaintenanceComplianceDto(decimal? Percentage, int CompletedCount,
    int OverdueCount, int PendingCount, int TotalDueCount);

public sealed record TechnicianWorkloadRowDto(Guid TechnicianId, string EmployeeId, string TechnicianName,
    int Assigned, int Started, int Approved, int Rejected, int AwaitingApproval, int Overdue,
    decimal? AverageExecutionMinutes, int BreakdownAssignments, int CorrectiveClosures);

public sealed record EscalationAlertDto(Guid WorkOrderId, string WorkOrderNumber, Guid MachineId,
    string MachineCode, string MachineName, DateOnly DueDate, int EscalationLevel,
    DateTime? LastEscalatedAt);

public sealed record UpcomingCalibrationDto(Guid MachineId, string MachineCode, string MachineName,
    Guid? CertificateId, string? CertificateNumber, DateOnly? ExpiryDate, int? DaysRemaining,
    CalibrationValidityStatus ValidityStatus);

public sealed record ExternalServiceFollowUpSummaryDto(Guid ExternalServiceId, string ServiceNumber,
    Guid MachineId, string MachineCode, string MachineName, string ServiceCompany,
    DateOnly DueDate, bool Overdue, string? Recommendation);

public sealed record ManagementDashboardDto(string Scope, ReportingPeriodDto Period,
    MachineKpiDto Machines, MaintenanceKpiDto Maintenance, BreakdownKpiDto Breakdowns,
    CalibrationKpiDto Calibration, int ExternalServiceFollowUpsDue,
    MaintenanceComplianceDto MaintenanceCompliance,
    IReadOnlyList<TechnicianWorkloadRowDto> TechnicianWorkload,
    IReadOnlyList<EscalationAlertDto> EscalationAlerts,
    IReadOnlyList<UpcomingCalibrationDto> UpcomingCalibrations,
    IReadOnlyList<ExternalServiceFollowUpSummaryDto> ExternalServiceFollowUps);

public sealed record TechnicianDashboardDto(ReportingPeriodDto Period, int DueToday, int Upcoming,
    int Overdue, int Rejected, int InProgress, int NotificationsUnread,
    int OpenBreakdownAssignments, int MyMachinesCount);

public sealed record PreventiveMaintenanceReportQuery(DateOnly? From = null, DateOnly? To = null,
    Guid? MachineId = null, Guid? DepartmentId = null, Guid? LocationId = null,
    Guid? TechnicianId = null, Guid? SupervisorId = null,
    WorkOrderLifecycleStatus? Status = null, MaintenancePriority? Priority = null,
    int Page = 1, int PageSize = 20);

public sealed record PreventiveMaintenanceReportRowDto(Guid WorkOrderId, string WorkOrderNumber,
    Guid MachineId, string MachineCode, string MachineName, string PlanName,
    DateOnly PlannedDate, DateOnly DueDate, DateTime? StartedAt, DateTime? CompletedAt,
    DateTime? SubmittedAt, DateTime? ApprovedAt, Guid? TechnicianId, string? Technician,
    Guid SupervisorId, string Supervisor, WorkOrderLifecycleStatus LifecycleStatus,
    MaintenancePriority Priority, int DaysOverdue, int EscalationLevel, DateTime? LastEscalationDate);

public sealed record OverdueMaintenanceReportQuery(DateOnly? AsOf = null, DateOnly? DueFrom = null,
    Guid? MachineId = null, Guid? TechnicianId = null, Guid? SupervisorId = null,
    int? EscalationLevel = null, Guid? DepartmentId = null, Guid? LocationId = null,
    int Page = 1, int PageSize = 20);

public sealed record OverdueMaintenanceReportRowDto(Guid WorkOrderId, string WorkOrderNumber,
    Guid MachineId, string MachineCode, string MachineName, Guid? TechnicianId, string? Technician,
    Guid SupervisorId, string Supervisor, DateOnly DueDate,
    WorkOrderLifecycleStatus LifecycleStatus, int DaysOverdue, int EscalationLevel,
    DateTime? LastEscalationDate, string SubmissionState);

public sealed record BreakdownReportQuery(DateOnly? From = null, DateOnly? To = null,
    Guid? MachineId = null, BreakdownSeverity? Severity = null, Guid? TechnicianId = null,
    Guid? SupervisorId = null, BreakdownStatus? Status = null, bool? MachineStopped = null,
    Guid? DepartmentId = null, Guid? LocationId = null, int Page = 1, int PageSize = 20);

public sealed record BreakdownReportRowDto(Guid BreakdownId, string BreakdownNumber,
    Guid MachineId, string MachineCode, string MachineName, DateTime ReportedAt,
    BreakdownSeverity Severity, bool MachineStopped, decimal DowntimeMinutes,
    string? RootCause, string? CorrectiveAction, Guid? TechnicianId, string? Technician,
    Guid SupervisorId, string Supervisor, BreakdownStatus FinalStatus, DateTime? ReturnedToServiceAt);

public sealed record CalibrationReportQuery(Guid? MachineId = null, string? Provider = null,
    CalibrationValidityStatus? ValidityStatus = null,
    CalibrationRenewalStatus? RenewalStatus = null, DateOnly? ExpiryFrom = null,
    DateOnly? ExpiryTo = null, Guid? DepartmentId = null, Guid? LocationId = null,
    int Page = 1, int PageSize = 20);

public sealed record CalibrationReportRowDto(Guid MachineId, string MachineCode, string MachineName,
    Guid? CertificateId, string? CertificateNumber, string? Provider,
    DateOnly? CalibrationDate, DateOnly? ExpiryDate, int? DaysRemaining,
    CalibrationResult? Result, CalibrationValidityStatus ValidityStatus,
    CalibrationRenewalStatus RenewalStatus);

public sealed record TechnicianWorkloadReportQuery(DateOnly? From = null, DateOnly? To = null,
    Guid? TechnicianId = null, Guid? DepartmentId = null, Guid? LocationId = null,
    int Page = 1, int PageSize = 20);

public enum ExternalServiceFollowUpStatus { NONE, UPCOMING, DUE, OVERDUE }

public sealed record ExternalServiceReportQuery(DateOnly? From = null, DateOnly? To = null,
    Guid? MachineId = null, string? Company = null, ExternalServiceType? ServiceType = null,
    ExternalServiceFollowUpStatus? FollowUpStatus = null,
    Guid? DepartmentId = null, Guid? LocationId = null, int Page = 1, int PageSize = 20);

public sealed record ExternalServiceReportRowDto(Guid ExternalServiceId, string ServiceNumber,
    Guid MachineId, string MachineCode, string MachineName, string Company, string? Technician,
    DateOnly ServiceDate, ExternalServiceType ServiceType, string? Findings, decimal? Cost,
    string? PurchaseOrderNumber, string? InvoiceNumber, DateOnly? FollowUpDate,
    DateOnly? NextServiceDate, ExternalServiceFollowUpStatus FollowUpStatus, int AttachmentCount);

public sealed record MachineHistoryReportQuery(DateOnly? From = null, DateOnly? To = null,
    string? Module = null, int Page = 1, int PageSize = 50);

public sealed record MachineHistoryEventDto(string Module, string EventType, DateTime OccurredAt,
    Guid SourceId, string Reference, string Summary, string? Details);

public sealed record MachineHistoryReportDto(Guid MachineId, string MachineCode, string MachineName,
    PagedResult<MachineHistoryEventDto> Events);

public sealed record MonthlySummaryQuery(int Year, int Month,
    Guid? DepartmentId = null, Guid? LocationId = null);

public sealed record MonthlyMetricsDto(int Year, int Month, int PlannedMaintenance,
    int ApprovedMaintenance, int OverdueMaintenance, MaintenanceComplianceDto Compliance,
    int BreakdownCount, int CriticalBreakdownCount, decimal TotalDowntimeMinutes,
    int ExternalServicesCount, int CalibrationExpired, int CalibrationExpiring,
    int Escalations, IReadOnlyList<TechnicianWorkloadRowDto> TechnicianWorkload);

public sealed record MonthlySummaryDto(MonthlyMetricsDto Current, MonthlyMetricsDto Previous);

public sealed record TrendQuery(DateOnly? From = null, DateOnly? To = null,
    Guid? DepartmentId = null, Guid? LocationId = null);

public sealed record MaintenanceTrendPointDto(int Year, int Month, decimal? CompliancePercentage,
    int Approved, int Overdue);

public sealed record BreakdownTrendPointDto(int Year, int Month, int BreakdownCount,
    decimal DowntimeMinutes);

public sealed record TechnicianWorkloadTrendPointDto(int Year, int Month, int Assigned,
    int Approved, int Overdue);

public sealed record CalibrationStatusDistributionDto(CalibrationValidityStatus Status, int Count);

public sealed record ReportingTrendsDto(ReportingPeriodDto Period,
    IReadOnlyList<MaintenanceTrendPointDto> Maintenance,
    IReadOnlyList<BreakdownTrendPointDto> Breakdowns,
    IReadOnlyList<TechnicianWorkloadTrendPointDto> TechnicianWorkload,
    IReadOnlyList<CalibrationStatusDistributionDto> CalibrationStatus);

public enum ReportCellKind { Text, Integer, Decimal, Date, DateTime, Boolean, Percentage }
public enum ReportExportFormat { Pdf, Excel }

public sealed record ReportColumn(string Header, ReportCellKind Kind = ReportCellKind.Text);

public sealed record ReportTable(string ReportName, string Title, string FileStem,
    string FilterSummary, IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows);

public sealed record ReportExportRequest(ReportTable Table, DateTime GeneratedAtUtc,
    string GeneratedBy);

public sealed record ReportExportFile(byte[] Content, string ContentType, string FileName,
    int RowCount);

public sealed record ReportExportHistoryQuery(DateOnly? From = null, DateOnly? To = null,
    Guid? GeneratedByUserId = null, int Page = 1, int PageSize = 20);

public sealed record ReportExportHistoryDto(Guid AuditId, string ReportName, string Format,
    int RowCount, string FileName, string FilterSummary, Guid GeneratedByUserId,
    string GeneratedBy, DateTime GeneratedAtUtc);

