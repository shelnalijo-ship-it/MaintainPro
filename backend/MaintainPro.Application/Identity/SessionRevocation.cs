using MaintainPro.Application.Abstractions;
using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Identity;

internal static class SessionRevocation
{
    internal static async Task RevokeUserAsync(IApplicationDbContext db, User user, string? ipAddress,
        DateTime now, CancellationToken cancellationToken)
    {
        user.SecurityStamp = Guid.NewGuid();
        var sessions = await db.RefreshTokens.Where(token => token.UserId == user.Id && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in sessions)
        {
            token.RevokedAt = now;
            token.RevokedByIp = ipAddress;
        }
    }
}
