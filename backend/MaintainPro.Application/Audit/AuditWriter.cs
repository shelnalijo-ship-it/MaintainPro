using System.Text.Json;
using System.Text.Json.Nodes;
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
            EntityId = entityId?.ToString(), OldValuesJson = Serialize(oldValues),
            NewValuesJson = Serialize(newValues), IpAddress = actor.IpAddress,
            DeviceInfo = actor.DeviceInfo is { Length: > 512 } device ? device[..512] : actor.DeviceInfo,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        });
    }

    private static string? Serialize(object? value)
    {
        if (value is null) return null;
        var node = JsonSerializer.SerializeToNode(value);
        Redact(node);
        return node?.ToJsonString();
    }

    private static void Redact(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(item => item.Key).ToArray())
            {
                if (key.Contains("password", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("token", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("signingkey", StringComparison.OrdinalIgnoreCase))
                    obj.Remove(key);
                else Redact(obj[key]);
            }
        }
        else if (node is JsonArray array)
            foreach (var child in array) Redact(child);
    }
}
