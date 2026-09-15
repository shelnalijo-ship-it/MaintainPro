using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class CorrectiveSubmissionAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CorrectiveSubmissionId { get; set; }
    public Guid FileId { get; set; }
    public BreakdownEvidenceType EvidenceType { get; set; }
    public string? Description { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }
    public CorrectiveSubmission Submission { get; set; } = null!;
    public FileRecord File { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
}
