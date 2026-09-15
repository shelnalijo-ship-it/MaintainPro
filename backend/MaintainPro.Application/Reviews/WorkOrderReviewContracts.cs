using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Reviews;

public sealed record WorkOrderReviewRequest(Guid SubmissionId, string? Remarks = null);

public sealed record PendingApprovalQuery(int Page = 1, int PageSize = 20,
    MaintenancePriority? Priority = null, DateOnly? SubmittedFrom = null, DateOnly? SubmittedTo = null,
    bool? Overdue = null, Guid? TechnicianId = null, Guid? MachineId = null);

public sealed record MachineMaintenanceHistoryQuery(int Page = 1, int PageSize = 20);

public sealed record WorkOrderReviewDto(Guid Id, Guid WorkOrderId, Guid WorkOrderSubmissionId,
    Guid SupervisorId, string SupervisorEmployeeId, string SupervisorName,
    WorkOrderReviewDecision Decision, string? Remarks, DateTime DecisionAt);

public sealed record SubmissionSummaryDto(Guid Id, Guid WorkOrderId, int VersionNumber,
    Guid SubmittedByUserId, string TechnicianEmployeeId, string TechnicianName,
    DateTime SubmittedAt, DateTime CompletedAt, decimal DurationMinutes, WorkOrderReviewDto? Review);

public sealed record WorkOrderSubmissionDto(SubmissionSummaryDto Submission, string? OverallComments,
    string? Observations, DateTime StartedAt, IReadOnlyList<SubmissionChecklistResultDto> ChecklistResults,
    IReadOnlyList<SubmissionAttachmentDto> Attachments, IReadOnlyList<SubmissionPartUsageDto> PartUsages,
    IReadOnlyList<SubmissionDefectDto> Defects);

public sealed record SubmissionChecklistResultDto(Guid Id, Guid WorkOrderChecklistItemId,
    int SequenceNumber, string Title, ChecklistResponseType ResponseType, bool IsMandatory,
    string? Unit, decimal? MinimumValue, decimal? MaximumValue, bool? BooleanValue,
    decimal? NumericValue, NumericReadingStatus? NumericRangeStatus, string? TextValue, PassFailResult? PassFailValue,
    bool? ConfirmationValue, string? Comment, DateTime? CompletedAt, Guid CompletedByUserId);

public sealed record SubmissionAttachmentDto(Guid Id, Guid FileId, Guid? WorkOrderChecklistItemId,
    string OriginalFilename, string MimeType, long FileSize, EvidenceType EvidenceType,
    string? Description, Guid UploadedByUserId, DateTime UploadedAt);

public sealed record SubmissionPartUsageDto(Guid Id, string PartName, string? PartNumber,
    decimal Quantity, string? Remarks, Guid CreatedByUserId, DateTime CreatedAt);

public sealed record SubmissionDefectDto(Guid Id, string Title, string Description, string? Severity,
    bool RequiresFollowUp, Guid CreatedByUserId, DateTime CreatedAt);

public sealed record PendingApprovalDto(Guid WorkOrderId, string WorkOrderNumber,
    Guid SubmissionId, int VersionNumber, Guid MachineId, string MachineCode, string MachineName,
    string PlanName, Guid TechnicianId, string TechnicianName, Guid SupervisorId,
    MaintenancePriority Priority, DateOnly PlannedDate, DateOnly DueDate, DateTime SubmittedAt,
    DateTime CompletedAt, bool ReviewOverdue);

public sealed record WorkOrderHistoryDto(Guid Id, int SequenceNumber, string Action,
    Guid? ActorUserId, string? ActorName, DateTime OccurredAt, Guid? WorkOrderSubmissionId,
    int? SubmissionVersion, string? Details);

public sealed record MachineMaintenanceHistoryDto(Guid WorkOrderId, string WorkOrderNumber,
    string PlanName, Guid TechnicianId, string TechnicianEmployeeId, string TechnicianName,
    Guid SupervisorId, string SupervisorEmployeeId, string SupervisorName,
    DateOnly PlannedDate, DateOnly DueDate, DateTime StartedAt, DateTime CompletedAt,
    DateTime SubmittedAt, DateTime ApprovedAt, WorkOrderLifecycleStatus LifecycleStatus,
    Guid ApprovedSubmissionId, int ApprovedSubmissionVersion, decimal DurationMinutes);
