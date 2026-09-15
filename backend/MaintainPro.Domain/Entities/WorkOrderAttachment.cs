using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class WorkOrderAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkOrderId { get; set; }
    public Guid FileId { get; set; }
    public Guid? WorkOrderChecklistItemId { get; set; }
    public EvidenceType EvidenceType { get; set; }
    public string? Description { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public WorkOrder WorkOrder { get; set; } = null!;
    public FileRecord File { get; set; } = null!;
    public WorkOrderChecklistItem? WorkOrderChecklistItem { get; set; }
    public User UploadedByUser { get; set; } = null!;
}
