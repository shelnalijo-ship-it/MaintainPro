using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.WorkOrders;

public sealed record GenerationRequest
{
    public DateOnly? ThroughDate { get; init; }
    public int MaxOccurrences { get; init; } = 500;
}

public sealed record GenerationIssue(Guid MaintenancePlanId, string Message);
public sealed record GenerationSummary(int PlansEvaluated, int WorkOrdersCreated, int Skipped,
    int Errors, bool HasMore, IReadOnlyList<GenerationIssue> Issues, int NotificationsCreated = 0);

public sealed record WorkOrderQuery(string? Search = null, int Page = 1, int PageSize = 20,
    Guid? MachineId = null, Guid? MaintenancePlanId = null, Guid? TechnicianId = null,
    Guid? SupervisorId = null, WorkOrderLifecycleStatus? LifecycleStatus = null,
    MaintenancePriority? Priority = null, DateOnly? PlannedFrom = null, DateOnly? PlannedTo = null,
    DateOnly? DueFrom = null, DateOnly? DueTo = null, bool? Overdue = null);

public sealed record WorkOrderCalendarQuery(DateOnly From, DateOnly To, Guid? MachineId = null,
    Guid? TechnicianId = null, Guid? SupervisorId = null, WorkOrderLifecycleStatus? LifecycleStatus = null);

public sealed record WorkOrderSummaryDto(Guid Id, string WorkOrderNumber, Guid MachineId,
    string MachineCode, string MachineName, Guid? MaintenancePlanId, string PlanName,
    Guid? AssignedTechnicianId, string? AssignedTechnicianName, Guid SupervisorId,
    string SupervisorName, DateOnly PlannedDate, DateOnly DueDate, MaintenancePriority Priority,
    WorkOrderLifecycleStatus LifecycleStatus, bool Overdue, int EscalationLevel,
    DateTime CreatedAt, DateTime UpdatedAt, bool IsDueSoon = false, int DaysOverdue = 0,
    DateTime? LastEscalatedAt = null, bool IsDueToday = false)
{
    public bool IsOverdue => Overdue;
}

public sealed record WorkOrderDto(WorkOrderSummaryDto WorkOrder, DateTime? StartedAt,
    DateTime? CompletedAt, DateTime? SubmittedAt, DateTime? ApprovedAt, DateTime? CancelledAt,
    WorkOrderDefinitionDto Definition);

public sealed record WorkOrderDefinitionDto(Guid Id, Guid MaintenanceTypeId, string MaintenanceTypeName,
    Guid ChecklistTemplateId, int ChecklistVersion, string ChecklistName, string PlanName,
    string? Instructions, int? EstimatedDurationMinutes, bool PhotoRequired, int MinimumPhotoCount,
    bool CommentRequired, MaintenancePriority Priority, string MachineCode, string MachineName,
    string? AssignedTechnicianEmployeeId, string? AssignedTechnicianName,
    string SupervisorEmployeeId, string SupervisorName, IReadOnlyList<WorkOrderChecklistItemDto> Items);

public sealed record WorkOrderChecklistItemDto(Guid Id, int SequenceNumber, string Title,
    string? Description, ChecklistResponseType ResponseType, bool IsMandatory, string? Unit,
    decimal? MinimumValue, decimal? MaximumValue, bool PhotoRequired);

public sealed record WorkOrderCalendarEventDto(Guid Id, string WorkOrderNumber, string PlanName,
    Guid MachineId, string MachineCode, string MachineName, Guid? AssignedTechnicianId,
    string? AssignedTechnicianName, Guid SupervisorId, string SupervisorName,
    DateOnly PlannedDate, DateOnly DueDate, MaintenancePriority Priority,
    WorkOrderLifecycleStatus LifecycleStatus, bool Overdue, bool IsDueSoon = false,
    int DaysOverdue = 0, int EscalationLevel = 0, DateTime? LastEscalatedAt = null, bool IsDueToday = false)
{
    public bool IsOverdue => Overdue;
}
