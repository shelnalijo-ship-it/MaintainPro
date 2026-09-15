using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MaintainPro.Application.Common;
using MaintainPro.Application.Identity;
using MaintainPro.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MaintainPro.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "MaintainPro";
    public string Audience { get; set; } = "MaintainPro.Client";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;

    public bool IsValid => !string.IsNullOrWhiteSpace(Issuer)
        && !string.IsNullOrWhiteSpace(Audience) && !string.IsNullOrWhiteSpace(SigningKey)
        && Encoding.UTF8.GetByteCount(SigningKey) >= 32
        && AccessTokenMinutes is >= 1 and <= 60 && RefreshTokenDays is >= 1 and <= 90;
}

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public AccessTokenValue CreateAccessToken(User user, Guid familyId)
    {
        var settings = Settings();
        var now = clock.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(settings.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("employee_id", user.EmployeeId),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("security_stamp", user.SecurityStamp.ToString()),
            new("sid", familyId.ToString())
        };
        claims.AddRange(user.UserRoles.Select(membership => membership.Role.Name)
            .Distinct().Select(role => new Claim("role", role)));
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            now, expiresAt, new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
        return new AccessTokenValue(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public RefreshTokenValue CreateRefreshToken()
    {
        var settings = Settings();
        var value = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        return new RefreshTokenValue(value, HashRefreshToken(value),
            clock.GetUtcNow().UtcDateTime.AddDays(settings.RefreshTokenDays));
    }

    public string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private JwtOptions Settings()
    {
        var settings = options.Value;
        if (!settings.IsValid)
            throw new AppException(503, "Authentication configuration is unavailable. Contact the administrator.");
        return settings;
    }
}
