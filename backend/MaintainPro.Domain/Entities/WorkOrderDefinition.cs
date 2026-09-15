using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class WorkOrderDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkOrderId { get; set; }
    public Guid MaintenanceTypeId { get; set; }
    public Guid ChecklistTemplateId { get; set; }
    public int ChecklistVersion { get; set; }
    public required string ChecklistName { get; set; }
    public required string PlanName { get; set; }
    public required string MaintenanceTypeName { get; set; }
    public string? Instructions { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public bool PhotoRequired { get; set; }
    public int MinimumPhotoCount { get; set; }
    public bool CommentRequired { get; set; }
    public MaintenancePriority Priority { get; set; }
    public required string MachineCode { get; set; }
    public required string MachineName { get; set; }
    public string? AssignedTechnicianEmployeeId { get; set; }
    public string? AssignedTechnicianName { get; set; }
    public required string SupervisorEmployeeId { get; set; }
    public required string SupervisorName { get; set; }
    public WorkOrder WorkOrder { get; set; } = null!;
    public MaintenanceType MaintenanceType { get; set; } = null!;
    public ChecklistTemplate ChecklistTemplate { get; set; } = null!;
    public ICollection<WorkOrderChecklistItem> Items { get; set; } = new List<WorkOrderChecklistItem>();
}
