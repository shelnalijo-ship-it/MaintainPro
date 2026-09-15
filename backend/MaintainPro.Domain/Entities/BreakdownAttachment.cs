using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class BreakdownAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BreakdownId { get; set; }
    public Guid FileId { get; set; }
    public BreakdownEvidenceType EvidenceType { get; set; }
    public string? Description { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public Breakdown Breakdown { get; set; } = null!;
    public FileRecord File { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
}
