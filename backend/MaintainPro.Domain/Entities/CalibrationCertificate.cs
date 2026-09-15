using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class CalibrationCertificate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MachineId { get; set; }
    public required string CertificateNumber { get; set; }
    public required string CalibrationProvider { get; set; }
    public DateOnly CalibrationDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public CalibrationResult Result { get; set; }
    public string? Remarks { get; set; }
    public Guid? CertificateFileId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
    public Machine Machine { get; set; } = null!;
    public FileRecord? CertificateFile { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
