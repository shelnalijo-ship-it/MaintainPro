using System.Text.Json;
using MaintainPro.Application.Notifications;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class NotificationInboxTests
{
    [Fact]
    public async Task Writer_requires_the_business_transaction_and_records_truthful_delivery_attempts()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.AsAdminAsync();
        var request = Request(user.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.NotificationWriter.EmitAsync(request));
        NotificationWriteResult result;
        await using (var transaction = await fixture.Db.BeginTransactionAsync())
        {
            result = await fixture.NotificationWriter.EmitAsync(request);
            await fixture.Db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        Assert.True(result.Created);
        var attempts = await fixture.Db.NotificationDeliveryAttempts.Where(x => x.NotificationId == result.NotificationId).ToListAsync();
        Assert.Equal(3, attempts.Count);
        Assert.Equal(NotificationDeliveryStatus.SENT, attempts.Single(x => x.Channel == NotificationChannel.IN_APP).Status);
        Assert.All(attempts.Where(x => x.Channel != NotificationChannel.IN_APP), attempt =>
        {
            Assert.Equal(NotificationDeliveryStatus.SKIPPED, attempt.Status);
            Assert.Null(attempt.ExternalMessageId);
            Assert.Contains("provider", attempt.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Writer_deduplicates_unsaved_and_persisted_events_and_rejects_cross_recipient_key_reuse()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.AsAdminAsync();
        var other = await fixture.SeedUserAsync("TECHNICIAN");
        var request = Request(user.Id);
        Guid notificationId;
        await using (var transaction = await fixture.Db.BeginTransactionAsync())
        {
            var first = await fixture.NotificationWriter.EmitAsync(request);
            var second = await fixture.NotificationWriter.EmitAsync(request);
            Assert.True(first.Created);
            Assert.False(second.Created);
            Assert.Equal(first.NotificationId, second.NotificationId);
            notificationId = first.NotificationId;
            await fixture.Db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        fixture.Db.ChangeTracker.Clear();
        await using (var transaction = await fixture.Db.BeginTransactionAsync())
        {
            var retry = await fixture.NotificationWriter.EmitAsync(request);
            Assert.False(retry.Created);
            Assert.Equal(notificationId, retry.NotificationId);
            await ModuleFixture.ExpectStatusAsync(409, () => fixture.NotificationWriter.EmitAsync(request with { UserId = other.Id }));
            await transaction.CommitAsync();
        }
        Assert.Equal(1, await fixture.Db.Notifications.CountAsync());
        Assert.Equal(3, await fixture.Db.NotificationDeliveryAttempts.CountAsync());
    }

    [Fact]
    public async Task Inbox_is_personal_filters_before_paging_and_keeps_read_state_idempotent()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.AsAdminAsync();
        var other = await fixture.SeedUserAsync("TECHNICIAN");
        var today = DateOnly.FromDateTime(fixture.Clock.GetUtcNow().UtcDateTime);
        var first = await EmitAsync(fixture, Request(user.Id, "first") with { Priority = NotificationPriority.HIGH });
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await EmitAsync(fixture, Request(user.Id, "second") with { NotificationType = NotificationType.WORK_ORDER_DUE });
        var foreign = await EmitAsync(fixture, Request(other.Id, "foreign"));
        var page = await fixture.Notifications.ListAsync(new(PageSize: 1));
        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Items);
        Assert.All(page.Items, item => Assert.Equal(user.Id, item.UserId));
        Assert.Equal(2, (await fixture.Notifications.UnreadCountAsync()).UnreadCount);
        Assert.Equal(first.NotificationId, Assert.Single((await fixture.Notifications.ListAsync(new(Priority: NotificationPriority.HIGH))).Items).Id);
        Assert.Equal(second.NotificationId, Assert.Single((await fixture.Notifications.ListAsync(new(Type: NotificationType.WORK_ORDER_DUE))).Items).Id);
        Assert.Equal(2, (await fixture.Notifications.ListAsync(new(From: today, To: today))).TotalCount);
        Assert.Empty((await fixture.Notifications.ListAsync(new(From: today.AddDays(1)))).Items);
        var read = await fixture.Notifications.MarkReadAsync(first.NotificationId);
        Assert.True(read.IsRead);
        var readAt = read.ReadAt;
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(readAt, (await fixture.Notifications.MarkReadAsync(first.NotificationId)).ReadAt);
        Assert.Equal(1, (await fixture.Notifications.UnreadCountAsync()).UnreadCount);
        Assert.Equal(first.NotificationId, Assert.Single((await fixture.Notifications.ListAsync(new(IsRead: true))).Items).Id);
        Assert.Equal(1, (await fixture.Notifications.ReadAllAsync()).UpdatedCount);
        Assert.Equal(0, (await fixture.Notifications.ReadAllAsync()).UpdatedCount);
        Assert.Equal(0, (await fixture.Notifications.UnreadCountAsync()).UnreadCount);
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Notifications.MarkReadAsync(foreign.NotificationId));
        fixture.ActAs(other);
        Assert.Equal(1, (await fixture.Notifications.UnreadCountAsync()).UnreadCount);
    }

    [Fact]
    public async Task Expired_notifications_are_hidden_and_cannot_be_marked_read()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.AsAdminAsync();
        var expired = await EmitAsync(fixture, Request(user.Id, "expired") with { ExpiresAt = fixture.Clock.GetUtcNow().UtcDateTime.AddMinutes(-1) });
        await EmitAsync(fixture, Request(user.Id, "active"));
        Assert.Equal(1, (await fixture.Notifications.ListAsync(new())).TotalCount);
        Assert.Equal(1, (await fixture.Notifications.UnreadCountAsync()).UnreadCount);
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Notifications.MarkReadAsync(expired.NotificationId));
        Assert.Equal(1, (await fixture.Notifications.ReadAllAsync()).UpdatedCount);
        Assert.False((await fixture.Db.Notifications.AsNoTracking().SingleAsync(x => x.Id == expired.NotificationId)).IsRead);
    }

    [Fact]
    public async Task Inbox_payload_does_not_expose_internal_delivery_keys_or_configuration()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.AsAdminAsync();
        await EmitAsync(fixture, Request(user.Id));
        var payload = JsonSerializer.Serialize(await fixture.Notifications.ListAsync(new()));
        Assert.DoesNotContain("DeduplicationKey", payload);
        Assert.DoesNotContain("ExternalMessageId", payload);
        Assert.DoesNotContain("SigningKey", payload);
        Assert.DoesNotContain(fixture.JwtSettings.SigningKey, payload);
    }

    [Theory]
    [InlineData("page")]
    [InlineData("size")]
    [InlineData("date")]
    [InlineData("type")]
    [InlineData("priority")]
    public async Task Inbox_rejects_invalid_query_values(string invalid)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var query = invalid switch
        {
            "page" => new NotificationQuery(Page: 0),
            "size" => new NotificationQuery(PageSize: 101),
            "date" => new NotificationQuery(From: new(2026, 9, 16), To: new(2026, 9, 15)),
            "type" => new NotificationQuery(Type: (NotificationType)999),
            _ => new NotificationQuery(Priority: (NotificationPriority)999)
        };
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Notifications.ListAsync(query));
    }

    [Fact]
    public async Task Inactive_recipient_is_rejected_and_unauthenticated_inbox_access_is_denied()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var user = await fixture.AsAdminAsync();
        user.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        await using (var transaction = await fixture.Db.BeginTransactionAsync())
            await ModuleFixture.ExpectStatusAsync(409, () => fixture.NotificationWriter.EmitAsync(Request(user.Id)));
        fixture.Actor.UserId = null;
        fixture.Actor.Roles = [];
        await ModuleFixture.ExpectStatusAsync(401, () => fixture.Notifications.ListAsync(new()));
        await ModuleFixture.ExpectStatusAsync(401, () => fixture.Notifications.ReadAllAsync());
    }

    internal static NotificationWriteRequest Request(Guid userId, string? key = null) => new(userId,
        NotificationType.WORK_ORDER_ASSIGNED, "Assigned maintenance", "An inspection is ready for execution.",
        NotificationPriority.NORMAL, $"tests:{key ?? Guid.NewGuid().ToString("N")}:{userId}");

    internal static async Task<NotificationWriteResult> EmitAsync(ModuleFixture fixture, NotificationWriteRequest request)
    {
        await using var transaction = await fixture.Db.BeginTransactionAsync();
        var result = await fixture.NotificationWriter.EmitAsync(request);
        await fixture.Db.SaveChangesAsync();
        await transaction.CommitAsync();
        return result;
    }
}
