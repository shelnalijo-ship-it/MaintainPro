using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Machines;

public sealed record MachineWriteRequest
{
    public string MachineCode { get; init; } = string.Empty;
    public string? AssetNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid? CategoryId { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public string? SerialNumber { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? LocationId { get; init; }
    public DateOnly? InstallationDate { get; init; }
    public DateOnly? CommissioningDate { get; init; }
    public DateOnly? WarrantyExpiryDate { get; init; }
    public MachineStatus Status { get; init; } = MachineStatus.Operational;
    public MachineCriticality Criticality { get; init; } = MachineCriticality.Medium;
    public bool CalibrationRequired { get; init; }
    public bool PreventiveMaintenanceRequired { get; init; }
    public Guid? MachineOwnerUserId { get; init; }
    public Guid? SupervisorUserId { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; } = true;
    public string? AssignmentReason { get; init; }
}

public sealed record MachineStatusRequest(MachineStatus Status, bool? IsActive = null);

/// <summary>Replaces the current owner and supervisor; null explicitly clears an assignment.</summary>
public sealed record MachineAssignmentRequest(
    Guid? MachineOwnerUserId, Guid? SupervisorUserId, string? Reason = null);

public sealed record MachineQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20,
    Guid? CategoryId = null,
    Guid? DepartmentId = null,
    Guid? LocationId = null,
    MachineStatus? Status = null,
    MachineCriticality? Criticality = null,
    bool? CalibrationRequired = null,
    Guid? OwnerUserId = null,
    Guid? SupervisorUserId = null,
    bool? IsActive = null);

public sealed record MachineDto(
    Guid Id,
    string MachineCode,
    string? AssetNumber,
    string Name,
    Guid? CategoryId,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    Guid? DepartmentId,
    Guid? LocationId,
    DateOnly? InstallationDate,
    DateOnly? CommissioningDate,
    DateOnly? WarrantyExpiryDate,
    MachineStatus Status,
    MachineCriticality Criticality,
    bool CalibrationRequired,
    bool PreventiveMaintenanceRequired,
    Guid? MachineOwnerUserId,
    Guid? SupervisorUserId,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    CalibrationValidityStatus CurrentCalibrationStatus = CalibrationValidityStatus.NOT_REQUIRED,
    string? CurrentCertificateNumber = null,
    DateOnly? CalibrationExpiryDate = null,
    int? DaysUntilCalibrationExpiry = null,
    bool RenewalInProgress = false);

public sealed record MachineAssignmentHistoryDto(
    Guid Id,
    Guid MachineId,
    Guid TechnicianId,
    Guid? SupervisorId,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    Guid AssignedByUserId,
    string? Reason);
