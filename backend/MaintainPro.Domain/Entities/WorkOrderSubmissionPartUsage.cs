using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class WorkOrderSubmissionPartUsage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkOrderSubmissionId { get; set; }
    public required string PartName { get; set; }
    public string? PartNumber { get; set; }
    public decimal Quantity { get; set; }
    public string? Remarks { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public WorkOrderSubmission Submission { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
