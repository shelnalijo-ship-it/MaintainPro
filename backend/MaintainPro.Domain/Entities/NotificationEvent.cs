using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class NotificationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkOrderId { get; set; }
    public NotificationType NotificationType { get; set; }
    public Guid? EventReferenceId { get; set; }
    public int AssignmentVersion { get; set; }
    public NotificationPriority Priority { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public required string DeduplicationKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public int EscalationLevel { get; set; }
    public WorkOrder WorkOrder { get; set; } = null!;
}
