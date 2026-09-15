using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class WorkOrderExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkOrderId { get; set; }
    public Guid TechnicianId { get; set; }
    public required string TechnicianEmployeeId { get; set; }
    public required string TechnicianName { get; set; }
    public string? OverallComments { get; set; }
    public string? Observations { get; set; }
    public DateTime? AttemptStartedAt { get; set; }
    public decimal AccumulatedDurationMinutes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public WorkOrder WorkOrder { get; set; } = null!;
    public User Technician { get; set; } = null!;
}
