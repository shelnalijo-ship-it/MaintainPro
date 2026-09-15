using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class FileRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string StorageKey { get; set; }
    public required string OriginalFilename { get; set; }
    public required string MimeType { get; set; }
    public long FileSize { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public User UploadedByUser { get; set; } = null!;
}
