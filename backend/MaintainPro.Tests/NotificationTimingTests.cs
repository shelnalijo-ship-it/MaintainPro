using MaintainPro.Application.Notifications;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Tests;

public sealed class NotificationTimingTests
{
    private static readonly DateTime Now = new(2026, 9, 15, 12, 30, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(2, false, false, false, 0, 0)]
    [InlineData(1, true, false, false, 0, 0)]
    [InlineData(0, false, true, false, 0, 0)]
    [InlineData(-1, false, false, true, 1, 1)]
    [InlineData(-2, false, false, true, 2, 1)]
    [InlineData(-3, false, false, true, 3, 2)]
    [InlineData(-4, false, false, true, 4, 2)]
    [InlineData(-5, false, false, true, 5, 3)]
    [InlineData(-10, false, false, true, 10, 3)]
    public void Timing_respects_default_due_windows_and_escalation_thresholds(int dueOffset,
        bool dueSoon, bool dueToday, bool overdue, int daysOverdue, int level)
    {
        var order = Order(DateOnly.FromDateTime(Now).AddDays(dueOffset));
        var result = new WorkOrderTimingService().Evaluate(order, Settings(), Now);
        Assert.Equal(dueSoon, result.IsDueSoon);
        Assert.Equal(dueToday, result.IsDueToday);
        Assert.Equal(overdue, result.IsOverdue);
        Assert.Equal(daysOverdue, result.DaysOverdue);
        Assert.Equal(level, result.EscalationLevel);
        Assert.Equal(WorkOrderLifecycleStatus.IN_PROGRESS, order.LifecycleStatus);
    }

    [Theory]
    [InlineData(WorkOrderLifecycleStatus.PLANNED)]
    [InlineData(WorkOrderLifecycleStatus.ASSIGNED)]
    [InlineData(WorkOrderLifecycleStatus.IN_PROGRESS)]
    [InlineData(WorkOrderLifecycleStatus.REJECTED)]
    [InlineData(WorkOrderLifecycleStatus.AWAITING_APPROVAL)]
    [InlineData(WorkOrderLifecycleStatus.APPROVED)]
    [InlineData(WorkOrderLifecycleStatus.CANCELLED)]
    public void Any_submitted_timestamp_permanently_suppresses_execution_reminders_regardless_of_lifecycle(WorkOrderLifecycleStatus state)
    {
        var order = Order(new(2026, 9, 1));
        order.LifecycleStatus = state;
        order.SubmittedAt = Now.AddDays(-1);
        var result = new WorkOrderTimingService().Evaluate(order, Settings(), Now);
        Assert.False(result.IsDueSoon);
        Assert.False(result.IsDueToday);
        Assert.False(result.IsOverdue);
        Assert.Equal(0, result.DaysOverdue);
        Assert.Equal(0, result.EscalationLevel);
        Assert.Equal(state, order.LifecycleStatus);
    }

    [Theory]
    [InlineData(WorkOrderLifecycleStatus.APPROVED)]
    [InlineData(WorkOrderLifecycleStatus.CANCELLED)]
    [InlineData(WorkOrderLifecycleStatus.AWAITING_APPROVAL)]
    public void Terminal_and_review_states_are_excluded_even_if_legacy_data_has_no_submission_timestamp(WorkOrderLifecycleStatus state)
    {
        var order = Order(new(2026, 9, 1));
        order.LifecycleStatus = state;
        var result = new WorkOrderTimingService().Evaluate(order, Settings(), Now);
        Assert.False(result.IsOverdue);
        Assert.Equal(0, result.EscalationLevel);
    }

    [Fact]
    public void Timing_changes_at_UTC_calendar_boundaries_not_elapsed_twenty_four_hour_periods()
    {
        var order = Order(new(2026, 9, 15));
        var service = new WorkOrderTimingService();
        Assert.True(service.Evaluate(order, Settings(), new DateTime(2026, 9, 14, 23, 59, 59, DateTimeKind.Utc)).IsDueSoon);
        Assert.True(service.Evaluate(order, Settings(), new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc)).IsDueToday);
        Assert.Equal(1, service.Evaluate(order, Settings(), new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc)).DaysOverdue);
    }

    [Fact]
    public void Configured_thresholds_and_equal_zero_thresholds_are_used_without_marking_today_overdue()
    {
        var service = new WorkOrderTimingService();
        var settings = Settings();
        settings.DueSoonDays = 4;
        settings.TechnicianOverdueDays = 2;
        settings.SupervisorEscalationDays = 4;
        settings.ManagerEscalationDays = 7;
        Assert.True(service.Evaluate(Order(new(2026, 9, 19)), settings, Now).IsDueSoon);
        Assert.Equal(0, service.Evaluate(Order(new(2026, 9, 14)), settings, Now).EscalationLevel);
        Assert.Equal(1, service.Evaluate(Order(new(2026, 9, 13)), settings, Now).EscalationLevel);
        Assert.Equal(2, service.Evaluate(Order(new(2026, 9, 11)), settings, Now).EscalationLevel);
        Assert.Equal(3, service.Evaluate(Order(new(2026, 9, 8)), settings, Now).EscalationLevel);
        settings.TechnicianOverdueDays = settings.SupervisorEscalationDays = settings.ManagerEscalationDays = 0;
        Assert.False(service.Evaluate(Order(new(2026, 9, 15)), settings, Now).IsOverdue);
        Assert.Equal(0, service.Evaluate(Order(new(2026, 9, 15)), settings, Now).EscalationLevel);
        Assert.Equal(3, service.Evaluate(Order(new(2026, 9, 14)), settings, Now).EscalationLevel);
    }

    private static WorkOrder Order(DateOnly dueDate) => new()
    {
        WorkOrderNumber = "WO-2026-0001", DueDate = dueDate,
        LifecycleStatus = WorkOrderLifecycleStatus.IN_PROGRESS
    };

    private static EscalationSettings Settings() => new()
    {
        DueSoonDays = 1, TechnicianOverdueDays = 1, SupervisorEscalationDays = 3, ManagerEscalationDays = 5
    };
}
