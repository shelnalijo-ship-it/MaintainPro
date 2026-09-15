using MaintainPro.Application.Abstractions;

namespace MaintainPro.Api.Security;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;
    public IReadOnlyCollection<string> Roles => accessor.HttpContext?.User.FindAll("role")
        .Select(claim => claim.Value).Distinct().ToArray() ?? [];
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? DeviceInfo => accessor.HttpContext?.Request.Headers.UserAgent.ToString();
}
