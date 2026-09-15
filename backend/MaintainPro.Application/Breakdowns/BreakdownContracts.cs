using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Breakdowns;

public sealed record BreakdownReportRequest(Guid MachineId, BreakdownSeverity Severity, bool MachineStopped,
    string Description, string? InitialObservation = null, Guid? SupervisorId = null);
public sealed record BreakdownAssignmentRequest(Guid TechnicianId, Guid? SupervisorId = null, string? Reason = null);
public sealed record BreakdownReturnRequest(string? Remarks = null);
public sealed record BreakdownQuery(string? Search = null, int Page = 1, int PageSize = 20,
    Guid? MachineId = null, Guid? TechnicianId = null, Guid? SupervisorId = null,
    BreakdownSeverity? Severity = null, BreakdownStatus? Status = null, bool? MachineStopped = null,
    DateOnly? ReportedFrom = null, DateOnly? ReportedTo = null, bool? OpenOnly = null);
public sealed record BreakdownDto(Guid Id, string BreakdownNumber, Guid MachineId, string MachineCode, string MachineName,
    Guid ReportedByUserId, string ReporterName, DateTime ReportedAt, BreakdownSeverity Severity,
    bool MachineStopped, string Description, string? InitialObservation, Guid? AssignedTechnicianId,
    string? TechnicianName, Guid SupervisorId, string SupervisorName, BreakdownStatus Status,
    MachineStatus? PreviousMachineStatus, DateTime? StartedAt, DateTime? CompletedAt, DateTime? SubmittedAt,
    DateTime? ClosedAt, DateTime? ReturnedToServiceAt, decimal DowntimeMinutes, bool DowntimeOngoing,
    DateTime CreatedAt, DateTime UpdatedAt, int SubmissionVersion);
public sealed record BreakdownReturnDto(Guid BreakdownId, Guid MachineId, MachineStatus MachineStatus,
    DateTime ReturnedToServiceAt, IReadOnlyList<Guid> ReturnedBreakdownIds);
public sealed record BreakdownHistoryDto(Guid Id, int SequenceNumber, string Action, Guid? ActorUserId,
    string? ActorName, DateTime OccurredAt, Guid? CorrectiveSubmissionId, int? SubmissionVersion, string? Details);
public sealed record BreakdownAssignmentDto(Guid Id, int SequenceNumber, Guid TechnicianId,
    string TechnicianEmployeeId, string TechnicianName, Guid SupervisorId, string SupervisorEmployeeId,
    string SupervisorName, Guid AssignedByUserId, DateTime AssignedAt, string? Reason);
