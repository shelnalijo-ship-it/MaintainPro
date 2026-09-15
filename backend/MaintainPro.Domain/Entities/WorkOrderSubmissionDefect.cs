using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class WorkOrderSubmissionDefect
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkOrderSubmissionId { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public string? Severity { get; set; }
    public bool RequiresFollowUp { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public WorkOrderSubmission Submission { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
