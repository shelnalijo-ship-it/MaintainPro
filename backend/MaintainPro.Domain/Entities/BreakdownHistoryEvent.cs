namespace MaintainPro.Domain.Entities;

public class BreakdownHistoryEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BreakdownId { get; set; }
    public int SequenceNumber { get; set; }
    public required string Action { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid? CorrectiveSubmissionId { get; set; }
    public int? SubmissionVersion { get; set; }
    public string? Details { get; set; }
    public Breakdown Breakdown { get; set; } = null!;
    public User? ActorUser { get; set; }
    public CorrectiveSubmission? Submission { get; set; }
}
