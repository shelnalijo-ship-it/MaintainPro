using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class CalibrationNotificationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MachineId { get; set; }
    public Guid? CalibrationCertificateId { get; set; }
    public Guid? CalibrationRenewalId { get; set; }
    public NotificationType NotificationType { get; set; }
    public NotificationPriority Priority { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public required string DeduplicationKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public Machine Machine { get; set; } = null!;
    public CalibrationCertificate? Certificate { get; set; }
    public CalibrationRenewal? Renewal { get; set; }
}
