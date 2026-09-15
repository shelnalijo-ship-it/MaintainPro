using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class WorkOrderEscalation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkOrderId { get; set; }
    public int Level { get; set; }
    public NotificationType TriggerType { get; set; }
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public Guid RecipientUserId { get; set; }
    public Guid NotificationId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public required string DeduplicationKey { get; set; }
    public string? RoutingReason { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public WorkOrder WorkOrder { get; set; } = null!;
    public User RecipientUser { get; set; } = null!;
    public Notification Notification { get; set; } = null!;
}
