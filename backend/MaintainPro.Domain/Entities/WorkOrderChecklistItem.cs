using MaintainPro.Domain.Enums;

namespace MaintainPro.Domain.Entities;

public class WorkOrderChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkOrderDefinitionId { get; set; }
    public int SequenceNumber { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public ChecklistResponseType ResponseType { get; set; }
    public bool IsMandatory { get; set; }
    public string? Unit { get; set; }
    public decimal? MinimumValue { get; set; }
    public decimal? MaximumValue { get; set; }
    public bool PhotoRequired { get; set; }
    public WorkOrderDefinition Definition { get; set; } = null!;
}
