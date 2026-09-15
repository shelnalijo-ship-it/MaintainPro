using MaintainPro.Application.Planning;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class NotificationWorkOrderReadTests
{
    [Fact]
    public async Task Reminder_processing_generates_upcoming_jobs_before_due_soon_dispatch_and_remains_idempotent()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var tomorrow = DateOnly.FromDateTime(fixture.Clock.GetUtcNow().UtcDateTime).AddDays(1);
        var data = await PlanningTestData.CreateAsync(fixture, tomorrow);
        var plan = await fixture.Plans.CreateAsync(data.Request with { FrequencyType = MaintenanceFrequencyType.YEARLY });
        await fixture.Plans.SetChecklistAsync(plan.Id, PlanningTestData.Checklist());
        Assert.Equal(0, (await fixture.Generation.GenerateDueAsync()).WorkOrdersCreated);
        Assert.Empty(await fixture.Db.WorkOrders.ToListAsync());

        var processed = await fixture.Reminders.ProcessDueAsync();

        Assert.Equal(0, processed.Errors);
        Assert.Equal(1, processed.GeneratedWorkOrders);
        Assert.Equal(2, processed.NotificationsCreated);
        var job = await fixture.Db.WorkOrders.SingleAsync();
        Assert.Equal(tomorrow, job.PlannedDate);
        Assert.Equal(WorkOrderLifecycleStatus.ASSIGNED, job.LifecycleStatus);
        Assert.Equal(new[] { NotificationType.WORK_ORDER_ASSIGNED, NotificationType.WORK_ORDER_DUE_SOON }.Order(),
            (await fixture.Db.Notifications.Select(x => x.NotificationType).ToListAsync()).Order());
        var repeated = await fixture.Reminders.ProcessDueAsync();
        Assert.Equal(0, repeated.GeneratedWorkOrders);
        Assert.Equal(0, repeated.NotificationsCreated);
    }

    [Fact]
    public async Task Due_soon_reads_apply_configured_window_consistently_to_details_lists_and_calendar()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture, 2);
        Assert.False((await fixture.WorkOrders.GetAsync(data.WorkOrder.Id)).WorkOrder.IsDueSoon);
        await fixture.EscalationSettings.UpdateAsync(new(2, 1, 3, 5));
        var detail = (await fixture.WorkOrders.GetAsync(data.WorkOrder.Id)).WorkOrder;
        var listed = Assert.Single((await fixture.WorkOrders.ListAsync(new())).Items);
        var calendar = Assert.Single(await fixture.WorkOrders.CalendarAsync(new(data.WorkOrder.PlannedDate, data.WorkOrder.PlannedDate)));
        Assert.True(detail.IsDueSoon);
        Assert.True(listed.IsDueSoon);
        Assert.True(calendar.IsDueSoon);
        Assert.False(detail.IsOverdue);
        Assert.Equal(0, detail.DaysOverdue);
        Assert.Equal(0, detail.EscalationLevel);
        Assert.Null(detail.LastEscalatedAt);
    }

    [Fact]
    public async Task Submitted_and_resumed_jobs_disappear_from_overdue_filters_but_preserve_escalation_history_dates()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture, -5);
        await fixture.SeedUserAsync("MANAGER");
        await fixture.Reminders.ProcessDueAsync();
        var before = (await fixture.WorkOrders.GetAsync(data.WorkOrder.Id)).WorkOrder;
        Assert.Equal(5, before.DaysOverdue);
        Assert.Equal(3, before.EscalationLevel);
        Assert.NotNull(before.LastEscalatedAt);
        Assert.Single((await fixture.WorkOrders.ListAsync(new(Overdue: true))).Items);
        var submission = await data.SubmitAsync(fixture);
        fixture.ActAs(data.Supervisor);
        await fixture.Reviews.RejectAsync(data.WorkOrder.Id, new(submission.Submission.Id, "Please correct the comment"));
        fixture.ActAs(data.Technician);
        await fixture.Execution.ResumeAsync(data.WorkOrder.Id);
        fixture.Clock.Advance(TimeSpan.FromDays(2));

        var after = (await fixture.WorkOrders.GetAsync(data.WorkOrder.Id)).WorkOrder;
        Assert.False(after.IsOverdue);
        Assert.False(after.IsDueSoon);
        Assert.Equal(0, after.DaysOverdue);
        Assert.Equal(0, after.EscalationLevel);
        Assert.Equal(before.LastEscalatedAt, after.LastEscalatedAt);
        Assert.Equal(WorkOrderLifecycleStatus.IN_PROGRESS, after.LifecycleStatus);
        Assert.Empty((await fixture.WorkOrders.ListAsync(new(Overdue: true))).Items);
        Assert.Single((await fixture.WorkOrders.ListAsync(new(Overdue: false))).Items);
        var calendar = Assert.Single(await fixture.WorkOrders.CalendarAsync(new(data.WorkOrder.PlannedDate, data.WorkOrder.PlannedDate)));
        Assert.False(calendar.IsOverdue);
        Assert.Equal(0, calendar.DaysOverdue);
        Assert.Equal(before.LastEscalatedAt, calendar.LastEscalatedAt);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public async Task Upcoming_generation_rejects_unbounded_horizons(int days)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Generation.GenerateUpcomingAsync(days));
    }
}
