using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public NotificationType NotificationType { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public bool IsRead { get; set; }
    public NotificationPriority Priority { get; set; }
    public required string DeduplicationKey { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public User User { get; set; } = null!;
}
