using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MaintainPro.Application.Audit;

internal static partial class AuditJsonSanitizer
{
    public static string? Serialize(object? value)
    {
        if (value is null) return null;
        var node = JsonSerializer.SerializeToNode(value);
        Redact(node);
        return node?.ToJsonString();
    }

    public static string? Sanitize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var node = JsonNode.Parse(json);
            Redact(node);
            return node?.ToJsonString();
        }
        catch (JsonException)
        {
            // Audit values are expected to be JSON. Do not echo malformed legacy content because
            // its structure and sensitivity cannot be established safely.
            return null;
        }
    }

    public static string? SanitizeMetadata(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (SensitiveText().IsMatch(trimmed) || JwtValue().IsMatch(trimmed)) return null;
        return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
    }

    private static void Redact(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(item => item.Key).ToArray())
            {
                if (SensitiveKey().IsMatch(key)) obj.Remove(key);
                else Redact(obj[key]);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array) Redact(child);
        }
        else if (node is JsonValue value && value.TryGetValue<string>(out var text)
                 && JwtValue().IsMatch(text))
        {
            value.ReplaceWith(JsonValue.Create("[REDACTED]"));
        }
    }

    [GeneratedRegex("password|token|secret|signing.?key|credential|authorization|api.?key|connection.?string", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveKey();

    [GeneratedRegex("(?:bearer\\s+)?[A-Za-z0-9_-]{8,}\\.[A-Za-z0-9_-]{8,}\\.[A-Za-z0-9_-]{8,}", RegexOptions.IgnoreCase)]
    private static partial Regex JwtValue();

    [GeneratedRegex("bearer\\s+|password|refresh.?token|access.?token|signing.?key|connection.?string", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveText();
}
