using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class MaintenancePlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MachineId { get; set; }
    public required string PlanName { get; set; }
    public Guid MaintenanceTypeId { get; set; }
    public MaintenancePriority Priority { get; set; } = MaintenancePriority.MEDIUM;
    public MaintenanceFrequencyType FrequencyType { get; set; }
    public int FrequencyValue { get; set; } = 1;
    public DateOnly StartDate { get; set; }
    public DateOnly NextDueDate { get; set; }
    public Guid? DefaultTechnicianId { get; set; }
    public Guid SupervisorId { get; set; }
    public string? Instructions { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public bool PhotoRequired { get; set; }
    public int MinimumPhotoCount { get; set; }
    public bool CommentRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
    public Machine Machine { get; set; } = null!;
    public MaintenanceType MaintenanceType { get; set; } = null!;
    public User? DefaultTechnician { get; set; }
    public User Supervisor { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<ChecklistTemplate> ChecklistTemplates { get; set; } = new List<ChecklistTemplate>();
}
