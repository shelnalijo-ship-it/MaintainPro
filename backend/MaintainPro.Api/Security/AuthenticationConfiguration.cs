using System.Text;
using MaintainPro.Application.Identity;
using MaintainPro.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MaintainPro.Api.Security;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddApplicationAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, configured) =>
            {
                var settings = configured.Value;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = settings.Issuer,
                    ValidateAudience = true, ValidAudience = settings.Audience,
                    ValidateLifetime = true, RequireExpirationTime = true,
                    RequireSignedTokens = true, ValidateIssuerSigningKey = true,
                    IssuerSigningKey = settings.IsValid
                        ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)) : null,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = "sub", RoleClaimType = "role"
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var userId)
                            || !Guid.TryParse(context.Principal?.FindFirst("security_stamp")?.Value, out var stamp)
                            || !Guid.TryParse(context.Principal?.FindFirst("sid")?.Value, out var family)
                            || !await context.HttpContext.RequestServices.GetRequiredService<AuthService>()
                                .ValidateAccessSessionAsync(userId, stamp, family, context.HttpContext.RequestAborted))
                            context.Fail("The session is no longer valid.");
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        await Results.Problem(statusCode: 401, title: "Authentication required",
                            detail: "A valid access token is required.").ExecuteAsync(context.HttpContext);
                    },
                    OnForbidden = context => Results.Problem(statusCode: 403, title: "Forbidden",
                        detail: "You do not have permission to perform this operation.").ExecuteAsync(context.HttpContext)
                };
            });
        Policies.AddPolicies(services);
        return services;
    }
}
