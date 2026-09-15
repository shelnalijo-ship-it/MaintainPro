using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class WorkOrderSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkOrderId { get; set; }
    public int VersionNumber { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public required string TechnicianEmployeeId { get; set; }
    public required string TechnicianName { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string? OverallComments { get; set; }
    public string? Observations { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public decimal DurationMinutes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public WorkOrder WorkOrder { get; set; } = null!;
    public User SubmittedByUser { get; set; } = null!;
    public WorkOrderApproval? Review { get; set; }
    public ICollection<WorkOrderSubmissionChecklistResult> ChecklistResults { get; set; } = new List<WorkOrderSubmissionChecklistResult>();
    public ICollection<WorkOrderSubmissionAttachment> Attachments { get; set; } = new List<WorkOrderSubmissionAttachment>();
    public ICollection<WorkOrderSubmissionPartUsage> PartUsages { get; set; } = new List<WorkOrderSubmissionPartUsage>();
    public ICollection<WorkOrderSubmissionDefect> Defects { get; set; } = new List<WorkOrderSubmissionDefect>();
}
