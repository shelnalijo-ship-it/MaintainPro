namespace MaintainPro.Domain.Entities;

public class MachineAssignmentHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MachineId { get; set; }
    public Guid TechnicianId { get; set; }
    public Guid? SupervisorId { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public Guid AssignedByUserId { get; set; }
    public string? Reason { get; set; }

    public Machine Machine { get; set; } = null!;
    public User Technician { get; set; } = null!;
    public User? Supervisor { get; set; }
    public User AssignedByUser { get; set; } = null!;
}
