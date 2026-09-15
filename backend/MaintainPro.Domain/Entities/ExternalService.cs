using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class ExternalService
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ServiceNumber { get; set; }
    public Guid MachineId { get; set; }
    public required string MachineCode { get; set; }
    public required string MachineName { get; set; }
    public required string ServiceCompany { get; set; }
    public string? ServiceTechnician { get; set; }
    public DateOnly ServiceDate { get; set; }
    public ExternalServiceType ServiceType { get; set; }
    public required string Description { get; set; }
    public string? Findings { get; set; }
    public string? WorkCompleted { get; set; }
    public string? PartsReplaced { get; set; }
    public decimal? Cost { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateOnly? FollowUpDate { get; set; }
    public DateOnly? NextServiceDate { get; set; }
    public string? Recommendation { get; set; }
    public string? Comments { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
    public Machine Machine { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<ExternalServiceAttachment> Attachments { get; set; } = new List<ExternalServiceAttachment>();
}
