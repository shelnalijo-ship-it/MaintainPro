using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class CorrectiveApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BreakdownId { get; set; }
    public Guid CorrectiveSubmissionId { get; set; }
    public Guid SupervisorId { get; set; }
    public required string SupervisorEmployeeId { get; set; }
    public required string SupervisorName { get; set; }
    public CorrectiveReviewDecision Decision { get; set; }
    public string? Remarks { get; set; }
    public DateTime DecisionAt { get; set; }
    public Breakdown Breakdown { get; set; } = null!;
    public CorrectiveSubmission Submission { get; set; } = null!;
    public User Supervisor { get; set; } = null!;
}
