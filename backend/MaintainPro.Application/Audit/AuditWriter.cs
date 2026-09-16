using MaintainPro.Application.Abstractions;
using MaintainPro.Domain.Entities;

namespace MaintainPro.Application.Audit;

public sealed class AuditWriter(IApplicationDbContext db, ICurrentUser actor, TimeProvider clock) : IAuditWriter
{
    public void Record(string action, string entityType, Guid? entityId,
        object? oldValues = null, object? newValues = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = actor.UserId, Action = action, EntityType = entityType,
            EntityId = entityId?.ToString(), OldValuesJson = AuditJsonSanitizer.Serialize(oldValues),
            NewValuesJson = AuditJsonSanitizer.Serialize(newValues), IpAddress = actor.IpAddress,
            DeviceInfo = actor.DeviceInfo is { Length: > 512 } device ? device[..512] : actor.DeviceInfo,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        });
    }
}
