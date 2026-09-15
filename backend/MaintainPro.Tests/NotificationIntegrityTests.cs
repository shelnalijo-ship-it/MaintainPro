using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class NotificationIntegrityTests
{
    [Theory]
    [InlineData("Notification")]
    [InlineData("DeliveryAttempt")]
    [InlineData("Event")]
    [InlineData("Escalation")]
    [InlineData("Settings")]
    public async Task Notification_and_escalation_records_cannot_be_deleted_through_entity_updates(string kind)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await ResolvedAsync(fixture);
        fixture.Db.ChangeTracker.Clear();
        object entity = kind switch
        {
            "Notification" => await fixture.Db.Notifications.FirstAsync(),
            "DeliveryAttempt" => await fixture.Db.NotificationDeliveryAttempts.FirstAsync(),
            "Event" => await fixture.Db.NotificationEvents.FirstAsync(),
            "Escalation" => await fixture.Db.WorkOrderEscalations.FirstAsync(),
            _ => await fixture.Db.EscalationSettings.SingleAsync()
        };
        var keys = PrimaryKey(fixture, entity);
        fixture.Db.Remove(entity);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());

        fixture.Db.ChangeTracker.Clear();
        Assert.NotNull(await fixture.Db.FindAsync(entity.GetType(), keys));
    }

    [Fact]
    public async Task Marking_a_notification_read_does_not_allow_rewriting_its_original_message()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ResolvedAsync(fixture);
        var notification = await fixture.Db.Notifications.SingleAsync(x =>
            x.NotificationType == NotificationType.WORK_ORDER_ASSIGNED && x.UserId == data.Technician.Id);
        await fixture.Notifications.MarkReadAsync(notification.Id);

        await AssertProtectedChangeAsync(fixture, notification, nameof(Notification.Message),
            "An altered historical assignment message");
    }

    [Fact]
    public async Task The_first_read_timestamp_cannot_be_replaced_with_a_later_time()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ResolvedAsync(fixture);
        var notification = await fixture.Db.Notifications.SingleAsync(x =>
            x.NotificationType == NotificationType.WORK_ORDER_ASSIGNED && x.UserId == data.Technician.Id);
        await fixture.Notifications.MarkReadAsync(notification.Id);
        fixture.Clock.Advance(TimeSpan.FromHours(1));

        await AssertProtectedChangeAsync(fixture, notification, nameof(Notification.ReadAt),
            fixture.Clock.GetUtcNow().UtcDateTime);
    }

    [Theory]
    [InlineData("ProcessedAt")]
    [InlineData("LastError")]
    public async Task A_processed_event_cannot_be_reopened_or_have_its_outcome_rewritten(string property)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await ResolvedAsync(fixture);
        var notificationEvent = await fixture.Db.NotificationEvents.SingleAsync(x =>
            x.NotificationType == NotificationType.WORK_ORDER_ASSIGNED);
        Assert.NotNull(notificationEvent.ProcessedAt);

        await AssertProtectedChangeAsync(fixture, notificationEvent, property,
            property == nameof(NotificationEvent.ProcessedAt) ? null : "Changed delivery outcome");
    }

    [Fact]
    public async Task A_skipped_push_attempt_cannot_be_changed_to_a_fabricated_success()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await ResolvedAsync(fixture);
        var attempt = await fixture.Db.NotificationDeliveryAttempts.FirstAsync(x => x.Channel == NotificationChannel.PUSH);
        Assert.Equal(NotificationDeliveryStatus.SKIPPED, attempt.Status);

        await AssertProtectedChangeAsync(fixture, attempt, nameof(NotificationDeliveryAttempt.Status), NotificationDeliveryStatus.SENT);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_resolved_escalation_cannot_be_reopened_or_have_its_resolution_time_rewritten(bool replaceTime)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await ResolvedAsync(fixture);
        var escalation = await fixture.Db.WorkOrderEscalations.FirstAsync();
        Assert.NotNull(escalation.ResolvedAt);

        await AssertProtectedChangeAsync(fixture, escalation, nameof(WorkOrderEscalation.ResolvedAt),
            replaceTime ? fixture.Clock.GetUtcNow().UtcDateTime.AddDays(1) : null);
    }

    [Fact]
    public async Task A_resolved_escalation_retains_the_original_recipient()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ResolvedAsync(fixture);
        var escalation = await fixture.Db.WorkOrderEscalations.SingleAsync(x => x.RecipientUserId == data.Technician.Id);

        await AssertProtectedChangeAsync(fixture, escalation, nameof(WorkOrderEscalation.RecipientUserId), data.Supervisor.Id);
    }

    private static async Task<NotificationTestData> ResolvedAsync(ModuleFixture fixture)
    {
        var data = await NotificationTestData.CreateAsync(fixture, dueOffsetDays: -1);
        await fixture.EscalationSettings.UpdateAsync(new(1, 1, 3, 5));
        var processed = await fixture.Reminders.ProcessDueAsync();
        Assert.Equal(0, processed.Errors);
        Assert.Equal(2, processed.EscalationsCreated);
        await data.SubmitAsync(fixture);
        Assert.All(await fixture.Db.WorkOrderEscalations.AsNoTracking().ToListAsync(), x => Assert.NotNull(x.ResolvedAt));
        return data;
    }

    private static async Task AssertProtectedChangeAsync(ModuleFixture fixture, object entity, string property, object? value)
    {
        var entry = fixture.Db.Entry(entity);
        var original = entry.Property(property).CurrentValue;
        var keys = PrimaryKey(fixture, entity);
        entry.Property(property).CurrentValue = value;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());

        fixture.Db.ChangeTracker.Clear();
        var persisted = await fixture.Db.FindAsync(entity.GetType(), keys);
        Assert.NotNull(persisted);
        Assert.Equal(original, fixture.Db.Entry(persisted).Property(property).CurrentValue);
    }

    private static object?[] PrimaryKey(ModuleFixture fixture, object entity)
    {
        var entry = fixture.Db.Entry(entity);
        return entry.Metadata.FindPrimaryKey()!.Properties.Select(x => entry.Property(x.Name).CurrentValue).ToArray();
    }
}
