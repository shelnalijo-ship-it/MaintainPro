using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class Breakdown
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string BreakdownNumber { get; set; }
    public Guid MachineId { get; set; }
    public required string MachineCode { get; set; }
    public required string MachineName { get; set; }
    public Guid ReportedByUserId { get; set; }
    public required string ReporterEmployeeId { get; set; }
    public required string ReporterName { get; set; }
    public DateTime ReportedAt { get; set; }
    public BreakdownSeverity Severity { get; set; }
    public bool MachineStopped { get; set; }
    public required string Description { get; set; }
    public string? InitialObservation { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public string? TechnicianEmployeeId { get; set; }
    public string? TechnicianName { get; set; }
    public Guid SupervisorId { get; set; }
    public required string SupervisorEmployeeId { get; set; }
    public required string SupervisorName { get; set; }
    public BreakdownStatus Status { get; set; } = BreakdownStatus.REPORTED;
    public MachineStatus? PreviousMachineStatus { get; set; }
    // A shared status token identifies the uninterrupted stop episode. The actual previous
    // status above remains unchanged, including Breakdown on subsequent overlapping reports.
    public MachineStatus? RestoreMachineStatus { get; set; }
    public Guid? MachineStatusVersionAtStop { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? ReturnedToServiceAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
    public int AssignmentVersion { get; set; }
    public int SubmissionVersion { get; set; }
    public int HistoryVersion { get; set; }
    public Machine Machine { get; set; } = null!;
    public User ReportedByUser { get; set; } = null!;
    public User? AssignedTechnician { get; set; }
    public User Supervisor { get; set; } = null!;
}
