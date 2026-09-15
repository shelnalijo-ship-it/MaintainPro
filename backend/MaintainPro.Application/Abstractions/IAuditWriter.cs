namespace MaintainPro.Application.Abstractions;

public interface IAuditWriter
{
    void Record(string action, string entityType, Guid? entityId,
        object? oldValues = null, object? newValues = null);
}
