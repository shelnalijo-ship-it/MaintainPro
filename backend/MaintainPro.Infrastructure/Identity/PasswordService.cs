using MaintainPro.Application.Identity;
using MaintainPro.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;

namespace MaintainPro.Infrastructure.Identity;

public sealed class PasswordService : IPasswordService
{
    private static readonly object DummySync = new();
    private static User? cachedDummyUser;
    private readonly IPasswordHasher<User> hasher;
    private readonly User dummyUser;

    public PasswordService(IPasswordHasher<User> hasher)
    {
        this.hasher = hasher;
        // Warm a process-local dummy hash when the service is resolved. Unknown accounts
        // then incur the same standard password verification work as existing accounts.
        lock (DummySync)
            dummyUser = cachedDummyUser ??= CreateDummyUser(hasher);
    }

    public string Hash(User user, string password) => hasher.HashPassword(user, password);

    public PasswordCheck Verify(User? user, string password)
    {
        var candidate = user ?? dummyUser;
        var result = hasher.VerifyHashedPassword(candidate, candidate.PasswordHash, password);
        return new PasswordCheck(user is not null && result != PasswordVerificationResult.Failed,
            user is not null && result == PasswordVerificationResult.SuccessRehashNeeded);
    }

    private static User CreateDummyUser(IPasswordHasher<User> hasher)
    {
        var marker = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var user = new User
        {
            EmployeeId = marker, FirstName = marker, LastName = marker, Email = marker,
            PasswordHash = string.Empty, IsActive = false
        };
        user.PasswordHash = hasher.HashPassword(user,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(64)));
        return user;
    }
}
