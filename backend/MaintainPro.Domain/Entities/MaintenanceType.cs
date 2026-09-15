using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class MaintenanceType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
}
