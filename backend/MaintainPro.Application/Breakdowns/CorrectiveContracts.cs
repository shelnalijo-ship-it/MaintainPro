using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Breakdowns;

public sealed record CorrectiveActionRequest(string? RootCause = null, string? CorrectiveAction = null, string? Comments = null);
public sealed record CorrectivePartRequest(string PartName, decimal Quantity, string? PartNumber = null, string? Remarks = null);
public sealed record CorrectiveReviewRequest(Guid SubmissionId, string? Remarks = null);
public sealed record BreakdownEvidenceUploadRequest(string OriginalFilename, string MimeType, long FileSize,
    BreakdownEvidenceType EvidenceType, string? Description = null);
public sealed record CorrectivePartDto(Guid Id, string PartName, string? PartNumber, decimal Quantity,
    string? Remarks, Guid CreatedByUserId, DateTime CreatedAt);
public sealed record BreakdownAttachmentDto(Guid Id, Guid FileId, BreakdownEvidenceType EvidenceType,
    string? Description, string OriginalFilename, string MimeType, long FileSize, Guid UploadedByUserId, DateTime UploadedAt);
public sealed record CorrectiveExecutionDto(Guid BreakdownId, BreakdownStatus Status, Guid? TechnicianId,
    string? TechnicianEmployeeId, string? TechnicianName, string? RootCause, string? CorrectiveAction,
    string? Comments, DateTime? StartedAt, DateTime? CompletedAt, decimal DurationMinutes,
    decimal DowntimeMinutes, bool IsComplete, IReadOnlyList<CorrectivePartDto> Parts,
    IReadOnlyList<BreakdownAttachmentDto> Attachments);
public sealed record CorrectiveApprovalDto(Guid Id, Guid BreakdownId, Guid CorrectiveSubmissionId,
    Guid SupervisorId, string SupervisorEmployeeId, string SupervisorName, CorrectiveReviewDecision Decision,
    string? Remarks, DateTime DecisionAt);
public sealed record CorrectiveSubmissionSummaryDto(Guid Id, Guid BreakdownId, int VersionNumber,
    Guid TechnicianId, string TechnicianEmployeeId, string TechnicianName, DateTime StartedAt,
    DateTime CompletedAt, DateTime SubmittedAt, decimal DurationMinutes, decimal DowntimeMinutes,
    CorrectiveApprovalDto? Review);
public sealed record CorrectiveSubmissionDto(CorrectiveSubmissionSummaryDto Submission, string RootCause,
    string CorrectiveAction, string? Comments, IReadOnlyList<CorrectivePartDto> Parts,
    IReadOnlyList<BreakdownAttachmentDto> Attachments);
