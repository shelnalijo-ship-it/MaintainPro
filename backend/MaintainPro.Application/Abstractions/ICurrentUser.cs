namespace MaintainPro.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
    IReadOnlyCollection<string> Roles { get; }
    string? IpAddress { get; }
    string? DeviceInfo { get; }
}
