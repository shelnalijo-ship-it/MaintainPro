using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Calibrations;

public sealed record CalibrationCreateRequest(Guid MachineId, string CertificateNumber,
    string CalibrationProvider, DateOnly CalibrationDate, DateOnly ExpiryDate, CalibrationResult Result,
    string? Remarks = null, bool RecordForNonRequiredMachine = false);
public sealed record CalibrationFileUpload(string OriginalFilename, string MimeType, long FileSize);
public sealed record CalibrationQuery(string? Search = null, int Page = 1, int PageSize = 20,
    Guid? MachineId = null, string? Provider = null, CalibrationResult? Result = null,
    CalibrationValidityStatus? ValidityStatus = null, CalibrationRenewalStatus? RenewalStatus = null,
    DateOnly? ExpiryFrom = null, DateOnly? ExpiryTo = null, int? ExpiringWithinDays = null,
    bool? ExpiredOnly = null);
public sealed record CalibrationCertificateDto(Guid Id, Guid MachineId, string MachineCode, string MachineName,
    string CertificateNumber, string CalibrationProvider, DateOnly CalibrationDate, DateOnly ExpiryDate,
    CalibrationResult Result, string? Remarks, Guid? CertificateFileId, Guid CreatedByUserId,
    DateTime CreatedAt, int? DaysRemaining, CalibrationValidityStatus ValidityStatus,
    CalibrationRenewalStatus RenewalStatus);
public sealed record MachineCalibrationStatusDto(Guid MachineId, bool CalibrationRequired,
    CalibrationValidityStatus ValidityStatus, CalibrationRenewalStatus RenewalStatus,
    Guid? CurrentCertificateId, string? CurrentCertificateNumber, DateOnly? ExpiryDate, int? DaysRemaining);
public sealed record CalibrationSummaryDto(int TotalCalibrationRequiredMachines, int Valid,
    int ExpiringWithin60Days, int ExpiringWithin30Days, int ExpiringWithin7Days, int Expired,
    int RenewalInProgress);
public sealed record CalibrationRenewalStartRequest(string? Notes = null);
public sealed record CalibrationRenewalCompleteRequest(Guid CertificateId, string? Notes = null);
public sealed record CalibrationRenewalCancelRequest(string? Notes = null);
public sealed record CalibrationRenewalDto(Guid Id, Guid MachineId, Guid? PreviousCertificateId,
    CalibrationRenewalStatus Status, DateTime StartedAt, Guid StartedByUserId, DateTime? CompletedAt,
    Guid? CompletedCertificateId, string? Notes);
public sealed record CalibrationReminderSummary(int MachinesEvaluated, int NotificationsCreated,
    int DuplicatesSkipped, int ExpiredDetected, int Errors, IReadOnlyList<string> Issues);
