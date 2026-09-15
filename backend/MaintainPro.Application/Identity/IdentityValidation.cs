using System.Net.Mail;
using MaintainPro.Application.Common;

namespace MaintainPro.Application.Identity;

public static class IdentityValidation
{
    public static string EmployeeId(string? value)
    {
        var result = Guard.Required(value, "Employee ID", 100).ToUpperInvariant();
        if (result.Any(character => !char.IsAsciiLetterOrDigit(character)
            && character != '-' && character != '_' && character != '.'))
            throw new AppException(400, "Employee ID may contain only letters, numbers, dots, hyphens and underscores.");
        return result;
    }

    public static string Email(string? value)
    {
        var result = Guard.Required(value, "Email", 254).ToLowerInvariant();
        if (!MailAddress.TryCreate(result, out var address)
            || !string.Equals(address.Address, result, StringComparison.OrdinalIgnoreCase))
            throw new AppException(400, "A valid email address is required.");
        return result;
    }

    public static void Password(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 12 || value.Length > 128)
            throw new AppException(400, "Password must contain 12 to 128 characters.");
    }

    public static string? Mobile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Guard.Required(value, "Mobile", 50);
    }
}
