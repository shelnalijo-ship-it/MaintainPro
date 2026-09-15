using MaintainPro.Application.Common;

namespace MaintainPro.Application.Planning;

internal static class PlanningValidation
{
    public static string? Optional(string? value, string name, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > max) throw new AppException(400, $"{name} cannot exceed {max} characters.");
        return value;
    }
}
