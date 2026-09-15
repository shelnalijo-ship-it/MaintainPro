using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class WorkOrderHistoryEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkOrderId { get; set; }
    public int SequenceNumber { get; set; }
    public required string Action { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid? WorkOrderSubmissionId { get; set; }
    public string? Details { get; set; }
    public WorkOrder WorkOrder { get; set; } = null!;
    public User? ActorUser { get; set; }
    public WorkOrderSubmission? Submission { get; set; }
}
