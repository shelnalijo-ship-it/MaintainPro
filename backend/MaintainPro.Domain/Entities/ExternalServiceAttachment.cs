using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class ExternalServiceAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExternalServiceId { get; set; }
    public Guid FileId { get; set; }
    public ExternalServiceDocumentType DocumentType { get; set; }
    public string? Description { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public Guid Version { get; set; } = Guid.NewGuid();
    public ExternalService ExternalService { get; set; } = null!;
    public FileRecord File { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
}
