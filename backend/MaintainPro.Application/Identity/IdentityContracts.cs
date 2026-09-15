using MaintainPro.Application.Users;
using MaintainPro.Domain.Entities;

namespace MaintainPro.Application.Identity;

public sealed record LoginRequest(string Identifier, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record AuthResponse(string AccessToken, string RefreshToken,
    DateTime AccessTokenExpiresAt, UserDto User);
public sealed record PasswordCheck(bool Succeeded, bool NeedsRehash = false);
public sealed record AccessTokenValue(string Value, DateTime ExpiresAt);
public sealed record RefreshTokenValue(string Value, string Hash, DateTime ExpiresAt);

public interface IPasswordService
{
    string Hash(User user, string password);
    PasswordCheck Verify(User? user, string password);
}

public interface ITokenService
{
    AccessTokenValue CreateAccessToken(User user, Guid familyId);
    RefreshTokenValue CreateRefreshToken();
    string HashRefreshToken(string token);
}
