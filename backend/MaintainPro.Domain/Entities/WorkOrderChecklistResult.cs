using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class WorkOrderChecklistResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkOrderId { get; set; }
    public Guid WorkOrderChecklistItemId { get; set; }
    public bool? BooleanValue { get; set; }
    public decimal? NumericValue { get; set; }
    public string? TextValue { get; set; }
    public PassFailResult? PassFailValue { get; set; }
    public bool? ConfirmationValue { get; set; }
    public string? Comment { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid CompletedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public WorkOrder WorkOrder { get; set; } = null!;
    public WorkOrderChecklistItem WorkOrderChecklistItem { get; set; } = null!;
    public User CompletedByUser { get; set; } = null!;
}
