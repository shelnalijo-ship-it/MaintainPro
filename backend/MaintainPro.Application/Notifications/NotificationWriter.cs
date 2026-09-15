using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Notifications;

public sealed class NotificationWriter(IApplicationDbContext db, TimeProvider clock)
{
    public async Task<NotificationWriteResult> EmitAsync(NotificationWriteRequest request, CancellationToken ct = default)
    {
        if (!db.HasActiveTransaction)
            throw new InvalidOperationException("Notification persistence requires the business transaction.");
        if (!Enum.IsDefined(request.NotificationType) || !Enum.IsDefined(request.Priority))
            throw new AppException(400, "Notification type and priority must be valid.");
        var key = Guard.Required(request.DeduplicationKey, "DeduplicationKey", 500);
        var existing = db.Notifications.Local.SingleOrDefault(x => x.DeduplicationKey == key)
            ?? await db.Notifications.SingleOrDefaultAsync(x => x.DeduplicationKey == key, ct);
        if (existing is not null)
        {
            if (existing.UserId != request.UserId || existing.NotificationType != request.NotificationType ||
                existing.EntityType != request.EntityType || existing.EntityId != request.EntityId)
                throw new AppException(409, "The deduplication key belongs to a different notification event.");
            return new(existing.Id, false);
        }
        if (!await db.Users.AnyAsync(x => x.Id == request.UserId && x.IsActive, ct))
            throw new AppException(409, "Notification recipient must be an existing active user.");
        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value.Kind != DateTimeKind.Utc)
            throw new AppException(400, "ExpiresAt must be UTC.");
        var now = clock.GetUtcNow().UtcDateTime;
        var notification = new Notification
        {
            UserId = request.UserId,
            NotificationType = request.NotificationType,
            Title = Guard.Required(request.Title, "Title", 200),
            Message = Guard.Required(request.Message, "Message", 10000),
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            CreatedAt = now,
            Priority = request.Priority,
            DeduplicationKey = key,
            ExpiresAt = request.ExpiresAt
        };
        db.Notifications.Add(notification);
        // IN_APP is delivered by the persisted inbox row. External channels are explicitly unconfigured.
        foreach (var channel in new[] { NotificationChannel.IN_APP, NotificationChannel.PUSH, NotificationChannel.EMAIL })
            db.NotificationDeliveryAttempts.Add(new NotificationDeliveryAttempt
            {
                NotificationId = notification.Id,
                Channel = channel,
                AttemptNumber = 1,
                Status = channel == NotificationChannel.IN_APP ? NotificationDeliveryStatus.SENT : NotificationDeliveryStatus.SKIPPED,
                AttemptedAt = now,
                ErrorMessage = channel == NotificationChannel.IN_APP ? null : "No external delivery provider is configured."
            });
        return new(notification.Id, true);
    }
}
