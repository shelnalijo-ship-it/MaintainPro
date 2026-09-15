namespace MaintainPro.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string EmployeeId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public string? Mobile { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? DepartmentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();
    public Guid Version { get; set; } = Guid.NewGuid();

    public Department? Department { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
