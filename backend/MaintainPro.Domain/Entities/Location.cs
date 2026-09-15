namespace MaintainPro.Domain.Entities;

public class Location
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;

    public Department? Department { get; set; }
}
