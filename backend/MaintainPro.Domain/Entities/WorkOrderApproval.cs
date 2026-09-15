using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class WorkOrderApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkOrderId { get; set; }
    public Guid WorkOrderSubmissionId { get; set; }
    public Guid SupervisorId { get; set; }
    public required string SupervisorEmployeeId { get; set; }
    public required string SupervisorName { get; set; }
    public WorkOrderReviewDecision Decision { get; set; }
    public string? Remarks { get; set; }
    public DateTime DecisionAt { get; set; }
    public WorkOrder WorkOrder { get; set; } = null!;
    public WorkOrderSubmission Submission { get; set; } = null!;
    public User Supervisor { get; set; } = null!;
}
