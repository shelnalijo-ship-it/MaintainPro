using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class MachineDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MachineId { get; set; }
    public Guid FileId { get; set; }
    public MachineDocumentType DocumentType { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateOnly? DocumentDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public Guid Version { get; set; } = Guid.NewGuid();
    public Machine Machine { get; set; } = null!;
    public FileRecord File { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
}
