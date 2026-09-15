using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class CalibrationRenewal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MachineId { get; set; }
    public Guid? PreviousCertificateId { get; set; }
    public CalibrationRenewalStatus Status { get; set; } = CalibrationRenewalStatus.IN_PROGRESS;
    public DateTime StartedAt { get; set; }
    public Guid StartedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedCertificateId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
    public Machine Machine { get; set; } = null!;
    public CalibrationCertificate? PreviousCertificate { get; set; }
    public CalibrationCertificate? CompletedCertificate { get; set; }
    public User StartedByUser { get; set; } = null!;
}
