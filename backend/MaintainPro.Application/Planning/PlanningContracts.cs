using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Planning;

public sealed record MaintenanceTypeQuery(bool? IsActive = null, string? Search = null);
public sealed record MaintenanceTypeWriteRequest(string Name, string? Description = null, bool IsActive = true);
public sealed record MaintenanceTypeStatusRequest(bool IsActive);
public sealed record MaintenanceTypeDto(Guid Id, string Name, string? Description, bool IsActive,
    DateTime CreatedAt, DateTime UpdatedAt);

public sealed record PlanQuery(string? Search = null, int Page = 1, int PageSize = 20,
    Guid? MachineId = null, Guid? MaintenanceTypeId = null, Guid? SupervisorId = null,
    Guid? DefaultTechnicianId = null, bool? IsActive = null);

public sealed record PlanWriteRequest(
    Guid MachineId,
    string PlanName,
    Guid MaintenanceTypeId,
    MaintenancePriority Priority,
    MaintenanceFrequencyType FrequencyType,
    int FrequencyValue,
    DateOnly StartDate,
    Guid SupervisorId,
    Guid? DefaultTechnicianId = null,
    string? Instructions = null,
    int? EstimatedDurationMinutes = null,
    bool PhotoRequired = false,
    int MinimumPhotoCount = 0,
    bool CommentRequired = false,
    bool IsActive = true);

public sealed record DuplicatePlanRequest(string PlanName, DateOnly StartDate, Guid? MachineId = null);
public sealed record PlanStatusRequest(bool IsActive);

public sealed record MaintenancePlanDto(
    Guid Id,
    Guid MachineId,
    string PlanName,
    Guid MaintenanceTypeId,
    MaintenancePriority Priority,
    MaintenanceFrequencyType FrequencyType,
    int FrequencyValue,
    DateOnly StartDate,
    DateOnly NextDueDate,
    Guid? DefaultTechnicianId,
    Guid SupervisorId,
    string? Instructions,
    int? EstimatedDurationMinutes,
    bool PhotoRequired,
    int MinimumPhotoCount,
    bool CommentRequired,
    bool IsActive,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ChecklistWriteRequest(string Name, IReadOnlyList<ChecklistItemWriteRequest> Items);

public sealed record ChecklistItemWriteRequest(
    int SequenceNumber,
    string Title,
    ChecklistResponseType ResponseType,
    bool IsMandatory = true,
    string? Description = null,
    string? Unit = null,
    decimal? MinimumValue = null,
    decimal? MaximumValue = null,
    bool PhotoRequired = false);

public sealed record ChecklistTemplateDto(
    Guid Id,
    Guid MaintenancePlanId,
    int Version,
    string Name,
    bool IsActive,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    IReadOnlyList<ChecklistItemDto> Items);

public sealed record ChecklistItemDto(
    Guid Id,
    Guid ChecklistTemplateId,
    int SequenceNumber,
    string Title,
    string? Description,
    ChecklistResponseType ResponseType,
    bool IsMandatory,
    string? Unit,
    decimal? MinimumValue,
    decimal? MaximumValue,
    bool PhotoRequired);
