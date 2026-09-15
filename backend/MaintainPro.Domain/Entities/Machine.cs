using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class Machine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string MachineCode { get; set; }
    public string? AssetNumber { get; set; }
    public required string Name { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? LocationId { get; set; }
    public DateOnly? InstallationDate { get; set; }
    public DateOnly? CommissioningDate { get; set; }
    public DateOnly? WarrantyExpiryDate { get; set; }
    public MachineStatus Status { get; set; } = MachineStatus.Operational;
    public MachineCriticality Criticality { get; set; } = MachineCriticality.Medium;
    public bool CalibrationRequired { get; set; }
    public bool PreventiveMaintenanceRequired { get; set; }
    public Guid? MachineOwnerUserId { get; set; }
    public Guid? SupervisorUserId { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
    public Guid StatusVersion { get; set; } = Guid.NewGuid();

    public MachineCategory? Category { get; set; }
    public Department? Department { get; set; }
    public Location? Location { get; set; }
    public User? MachineOwner { get; set; }
    public User? Supervisor { get; set; }
}
