using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.ExternalServices;

public sealed record ExternalServiceWriteRequest(Guid MachineId, string ServiceCompany,
    string? ServiceTechnician, DateOnly ServiceDate, ExternalServiceType ServiceType, string Description,
    string? Findings = null, string? WorkCompleted = null, string? PartsReplaced = null,
    decimal? Cost = null, string? PurchaseOrderNumber = null, string? InvoiceNumber = null,
    DateOnly? FollowUpDate = null, DateOnly? NextServiceDate = null,
    string? Recommendation = null, string? Comments = null);

public sealed record ExternalServiceQuery(string? Search = null, int Page = 1, int PageSize = 20,
    Guid? MachineId = null, string? ServiceCompany = null, ExternalServiceType? ServiceType = null,
    DateOnly? ServiceFrom = null, DateOnly? ServiceTo = null,
    DateOnly? FollowUpFrom = null, DateOnly? FollowUpTo = null,
    DateOnly? NextServiceFrom = null, DateOnly? NextServiceTo = null,
    bool? HasAttachments = null, string? PurchaseOrderNumber = null, string? InvoiceNumber = null);

public sealed record ExternalServiceDto(Guid Id, string ServiceNumber, Guid MachineId,
    string MachineCode, string MachineName, string ServiceCompany, string? ServiceTechnician,
    DateOnly ServiceDate, ExternalServiceType ServiceType, string Description, string? Findings,
    string? WorkCompleted, string? PartsReplaced, decimal? Cost, string? PurchaseOrderNumber,
    string? InvoiceNumber, DateOnly? FollowUpDate, DateOnly? NextServiceDate,
    string? Recommendation, string? Comments, Guid CreatedByUserId, DateTime CreatedAt,
    DateTime UpdatedAt, int ActiveAttachmentCount);

public sealed record ExternalServiceFollowUpQuery(DateOnly? DueBefore = null, bool OverdueOnly = false,
    Guid? MachineId = null, string? ServiceCompany = null, int Page = 1, int PageSize = 20);

public sealed record ExternalServiceFollowUpDto(Guid ExternalServiceId, string ServiceNumber,
    Guid MachineId, string MachineCode, string MachineName, string ServiceCompany,
    DateOnly? FollowUpDate, DateOnly? NextServiceDate, DateOnly DueDate,
    bool IsOverdue, string? Recommendation, Guid? MachineOwnerUserId, Guid? SupervisorUserId);

public sealed record ExternalServiceAttachmentUploadRequest(string OriginalFilename, string MimeType,
    long FileSize, ExternalServiceDocumentType DocumentType, string? Description = null);

public sealed record ExternalServiceAttachmentDto(Guid Id, Guid ExternalServiceId, Guid FileId,
    ExternalServiceDocumentType DocumentType, string? Description, string OriginalFilename,
    string MimeType, long FileSize, Guid UploadedByUserId, DateTime UploadedAt, bool IsActive);

public sealed record MachineDocumentCreateRequest(string OriginalFilename, string MimeType, long FileSize,
    MachineDocumentType DocumentType, string Title, string? Description = null,
    DateOnly? DocumentDate = null, DateOnly? ExpiryDate = null);

public sealed record MachineDocumentUpdateRequest(MachineDocumentType DocumentType, string Title,
    string? Description = null, DateOnly? DocumentDate = null, DateOnly? ExpiryDate = null);

public sealed record MachineDocumentStatusRequest(bool IsActive);

public sealed record MachineDocumentQuery(string? Search = null, int Page = 1, int PageSize = 20,
    MachineDocumentType? DocumentType = null, MachineDocumentExpiryStatus? ExpiryStatus = null,
    DateOnly? UploadedFrom = null, DateOnly? UploadedTo = null, bool? IsActive = null);

public sealed record MachineDocumentDto(Guid Id, Guid MachineId, Guid FileId,
    MachineDocumentType DocumentType, string Title, string? Description, DateOnly? DocumentDate,
    DateOnly? ExpiryDate, bool IsExpired, int? DaysUntilExpiry, bool IsExpiringSoon,
    MachineDocumentExpiryStatus ExpiryStatus, string OriginalFilename, string MimeType,
    long FileSize, Guid UploadedByUserId, DateTime UploadedAt, DateTime UpdatedAt, bool IsActive);
