using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class NotificationDeliveryAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationId { get; set; }
    public NotificationChannel Channel { get; set; }
    public int AttemptNumber { get; set; }
    public NotificationDeliveryStatus Status { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
    public string? ExternalMessageId { get; set; }
    public Notification Notification { get; set; } = null!;
}
