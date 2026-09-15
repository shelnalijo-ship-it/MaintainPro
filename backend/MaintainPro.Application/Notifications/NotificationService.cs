using System.Linq.Expressions;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Notifications;

public sealed class NotificationService(IApplicationDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    public async Task<PagedResult<NotificationDto>> ListAsync(NotificationQuery request, CancellationToken ct = default)
    {
        Guard.Page(request.Page, request.PageSize);
        if (request.Type.HasValue && !Enum.IsDefined(request.Type.Value)) throw new AppException(400, "Type is invalid.");
        if (request.Priority.HasValue && !Enum.IsDefined(request.Priority.Value)) throw new AppException(400, "Priority is invalid.");
        ValidateDates(request.From, request.To);
        var query = Inbox().AsNoTracking();
        if (request.IsRead.HasValue) query = query.Where(x => x.IsRead == request.IsRead);
        if (request.Type.HasValue) query = query.Where(x => x.NotificationType == request.Type);
        if (request.Priority.HasValue) query = query.Where(x => x.Priority == request.Priority);
        if (request.From.HasValue)
        {
            var from = request.From.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt >= from);
        }
        if (request.To.HasValue)
        {
            var to = request.To.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt <= to);
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(Projection).ToListAsync(ct);
        return new(items, request.Page, request.PageSize, total);
    }

    public async Task<NotificationUnreadCountDto> UnreadCountAsync(CancellationToken ct = default) =>
        new(await Inbox().AsNoTracking().CountAsync(x => !x.IsRead, ct));

    public async Task<NotificationDto> MarkReadAsync(Guid id, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var notification = await Inbox().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "Notification not found.");
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return ToDto(notification);
    }

    public async Task<NotificationReadAllDto> ReadAllAsync(CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var notifications = await Inbox().Where(x => !x.IsRead).ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var notification in notifications) { notification.IsRead = true; notification.ReadAt = now; }
        if (notifications.Count > 0) await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(notifications.Count);
    }

    private IQueryable<Notification> Inbox()
    {
        var actorId = Guard.Authenticated(currentUser);
        var now = clock.GetUtcNow().UtcDateTime;
        return db.Notifications.Where(x => x.UserId == actorId && (x.ExpiresAt == null || x.ExpiresAt > now));
    }

    internal static void ValidateDates(DateOnly? from, DateOnly? to)
    {
        if (from.HasValue && to.HasValue && from > to) throw new AppException(400, "From cannot follow To.");
    }
    private static readonly Expression<Func<Notification, NotificationDto>> Projection = x => new(x.Id,
        x.UserId, x.NotificationType, x.Title, x.Message, x.EntityType, x.EntityId,
        x.CreatedAt, x.ReadAt, x.IsRead, x.Priority, x.ExpiresAt);
    private static NotificationDto ToDto(Notification x) => new(x.Id, x.UserId, x.NotificationType,
        x.Title, x.Message, x.EntityType, x.EntityId, x.CreatedAt, x.ReadAt, x.IsRead, x.Priority, x.ExpiresAt);
}
