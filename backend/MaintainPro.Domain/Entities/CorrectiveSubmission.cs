namespace MaintainPro.Domain.Entities;

public class CorrectiveSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BreakdownId { get; set; }
    public int VersionNumber { get; set; }
    public Guid TechnicianId { get; set; }
    public required string TechnicianEmployeeId { get; set; }
    public required string TechnicianName { get; set; }
    public required string RootCause { get; set; }
    public required string CorrectiveAction { get; set; }
    public string? Comments { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public decimal DurationMinutes { get; set; }
    public decimal DowntimeMinutes { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Breakdown Breakdown { get; set; } = null!;
    public User Technician { get; set; } = null!;
    public CorrectiveApproval? Review { get; set; }
    public ICollection<CorrectiveSubmissionPartUsage> PartUsages { get; set; } = new List<CorrectiveSubmissionPartUsage>();
    public ICollection<CorrectiveSubmissionAttachment> Attachments { get; set; } = new List<CorrectiveSubmissionAttachment>();
}
