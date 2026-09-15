using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Execution;

public sealed record ChecklistResultRequest(Guid WorkOrderChecklistItemId, bool? BooleanValue = null,
    decimal? NumericValue = null, string? TextValue = null, PassFailResult? PassFailValue = null,
    bool? ConfirmationValue = null, string? Comment = null);
public sealed record ChecklistResultsRequest(IReadOnlyList<ChecklistResultRequest> Results);
public sealed record ExecutionCommentsRequest(string? OverallComments = null, string? Observations = null);
public sealed record PartUsageRequest(string PartName, decimal Quantity, string? PartNumber = null, string? Remarks = null);
public sealed record DefectRequest(string Title, string Description, string? Severity = null, bool RequiresFollowUp = false);
public sealed record EvidenceUploadRequest(string OriginalFilename, string MimeType, long FileSize,
    EvidenceType EvidenceType, Guid? WorkOrderChecklistItemId = null, string? Description = null);
public sealed record ChecklistResultDto(Guid Id, Guid WorkOrderChecklistItemId, bool? BooleanValue,
    decimal? NumericValue, string? TextValue, PassFailResult? PassFailValue, bool? ConfirmationValue,
    string? Comment, DateTime? CompletedAt, Guid CompletedByUserId, DateTime UpdatedAt,
    NumericReadingStatus? ReadingStatus);
public sealed record PartUsageDto(Guid Id, string PartName, string? PartNumber, decimal Quantity,
    string? Remarks, Guid CreatedByUserId, DateTime CreatedAt);
public sealed record DefectDto(Guid Id, string Title, string Description, string? Severity,
    bool RequiresFollowUp, Guid CreatedByUserId, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record AttachmentDto(Guid Id, Guid FileId, Guid? WorkOrderChecklistItemId,
    EvidenceType EvidenceType, string? Description, string OriginalFilename, string MimeType,
    long FileSize, Guid UploadedByUserId, DateTime UploadedAt);
public sealed record WorkOrderExecutionDto(Guid WorkOrderId, WorkOrderLifecycleStatus LifecycleStatus,
    string? OverallComments, string? Observations, DateTime? StartedAt, DateTime? CompletedAt,
    DateTime? SubmittedAt, DateTime? ApprovedAt, decimal DurationMinutes, bool IsComplete,
    Guid? TechnicianId, string? TechnicianEmployeeId, string? TechnicianName,
    IReadOnlyList<ChecklistResultDto> ChecklistResults, IReadOnlyList<PartUsageDto> Parts,
    IReadOnlyList<DefectDto> Defects, IReadOnlyList<AttachmentDto> Attachments);
public sealed record FileDownload(Stream Content, string OriginalFilename, string MimeType, long FileSize);
