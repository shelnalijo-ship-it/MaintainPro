namespace MaintainPro.Domain.Entities;

public class BreakdownAssignmentHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BreakdownId { get; set; }
    public int SequenceNumber { get; set; }
    public Guid TechnicianId { get; set; }
    public required string TechnicianEmployeeId { get; set; }
    public required string TechnicianName { get; set; }
    public Guid SupervisorId { get; set; }
    public required string SupervisorEmployeeId { get; set; }
    public required string SupervisorName { get; set; }
    public Guid AssignedByUserId { get; set; }
    public DateTime AssignedAt { get; set; }
    public string? Reason { get; set; }
    public Breakdown Breakdown { get; set; } = null!;
    public User Technician { get; set; } = null!;
    public User Supervisor { get; set; } = null!;
    public User AssignedByUser { get; set; } = null!;
}
