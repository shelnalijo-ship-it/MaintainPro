namespace MaintainPro.Domain.Entities;

public class CorrectivePartUsage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BreakdownId { get; set; }
    public required string PartName { get; set; }
    public string? PartNumber { get; set; }
    public decimal Quantity { get; set; }
    public string? Remarks { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Breakdown Breakdown { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
