using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class WorkOrderSubmissionAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkOrderSubmissionId { get; set; }
    public Guid FileId { get; set; }
    public Guid? WorkOrderChecklistItemId { get; set; }
    public EvidenceType EvidenceType { get; set; }
    public string? Description { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }
    public WorkOrderSubmission Submission { get; set; } = null!;
    public FileRecord File { get; set; } = null!;
    public WorkOrderChecklistItem? WorkOrderChecklistItem { get; set; }
    public User UploadedByUser { get; set; } = null!;
}
