using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class ChecklistTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaintenancePlanId { get; set; }
    public int Version { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public MaintenancePlan MaintenancePlan { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<ChecklistItem> Items { get; set; } = new List<ChecklistItem>();
}
