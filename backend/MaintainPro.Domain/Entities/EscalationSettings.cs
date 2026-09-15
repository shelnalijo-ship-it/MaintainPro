using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class EscalationSettings
{
    public int Id { get; set; } = 1;
    public int DueSoonDays { get; set; } = 1;
    public int TechnicianOverdueDays { get; set; } = 1;
    public int SupervisorEscalationDays { get; set; } = 3;
    public int ManagerEscalationDays { get; set; } = 5;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByUserId { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public User? UpdatedByUser { get; set; }
}
