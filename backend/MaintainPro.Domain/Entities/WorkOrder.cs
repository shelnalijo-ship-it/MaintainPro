using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class WorkOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string WorkOrderNumber { get; set; }
    public Guid MachineId { get; set; }
    public Guid? MaintenancePlanId { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public Guid SupervisorId { get; set; }
    public DateOnly PlannedDate { get; set; }
    public DateOnly DueDate { get; set; }
    public MaintenancePriority Priority { get; set; }
    public WorkOrderLifecycleStatus LifecycleStatus { get; set; } = WorkOrderLifecycleStatus.PLANNED;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int EscalationLevel { get; set; }
    public int SubmissionVersion { get; set; }
    public int HistoryVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
    public Machine Machine { get; set; } = null!;
    public MaintenancePlan? MaintenancePlan { get; set; }
    public User? AssignedTechnician { get; set; }
    public User Supervisor { get; set; } = null!;
    public WorkOrderDefinition Definition { get; set; } = null!;
}
