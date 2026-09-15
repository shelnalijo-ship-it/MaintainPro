namespace MaintainPro.Domain.Entities;

public class BreakdownNumberSequence
{
    public int Year { get; set; }
    public long LastValue { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
