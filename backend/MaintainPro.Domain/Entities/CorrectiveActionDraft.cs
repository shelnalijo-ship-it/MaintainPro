namespace MaintainPro.Domain.Entities;

public class CorrectiveActionDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BreakdownId { get; set; }
    public Guid TechnicianId { get; set; }
    public required string TechnicianEmployeeId { get; set; }
    public required string TechnicianName { get; set; }
    public string? RootCause { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? Comments { get; set; }
    public DateTime? AttemptStartedAt { get; set; }
    public decimal AccumulatedDurationMinutes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Breakdown Breakdown { get; set; } = null!;
    public User Technician { get; set; } = null!;
}
