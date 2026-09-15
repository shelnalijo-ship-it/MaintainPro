using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

/// <summary>A durable notification intent retained with its originating breakdown.</summary>
public class BreakdownNotificationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BreakdownId { get; set; }
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
    public Breakdown Breakdown { get; set; } = null!;
}
