using MaintainPro.Application.Notifications;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class NotificationReminderTests
{
    [Theory]
    [InlineData(1, NotificationType.WORK_ORDER_DUE_SOON, 0)]
    [InlineData(0, NotificationType.WORK_ORDER_DUE, 0)]
    [InlineData(-1, NotificationType.WORK_ORDER_OVERDUE, 1)]
    [InlineData(-3, NotificationType.ESCALATION_SUPERVISOR, 2)]
    [InlineData(-5, NotificationType.ESCALATION_MANAGER, 3)]
    public async Task Processing_creates_due_events_once_and_preserves_lifecycle(int dueOffset, NotificationType expected, int level)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture, dueOffset);
        await fixture.SeedUserAsync("MANAGER");
        var lifecycle = data.WorkOrder.LifecycleStatus;
        var first = await fixture.Reminders.ProcessDueAsync();
        Assert.Equal(0, first.Errors);
        Assert.Contains(await fixture.Db.Notifications.Select(x => x.NotificationType).ToListAsync(), type => type == expected);
        var persisted = await fixture.Db.WorkOrders.AsNoTracking().SingleAsync();
        Assert.Equal(lifecycle, persisted.LifecycleStatus);
        Assert.Equal(level, persisted.EscalationLevel);
        var notifications = await fixture.Db.Notifications.CountAsync();
        var escalations = await fixture.Db.WorkOrderEscalations.CountAsync();
        var history = await fixture.Db.WorkOrderHistoryEvents.CountAsync();
        var repeated = await fixture.Reminders.ProcessDueAsync();
        Assert.Equal(0, repeated.Errors);
        Assert.Equal(0, repeated.NotificationsCreated);
        Assert.Equal(0, repeated.EscalationsCreated);
        Assert.Equal(notifications, await fixture.Db.Notifications.CountAsync());
        Assert.Equal(escalations, await fixture.Db.WorkOrderEscalations.CountAsync());
        Assert.Equal(history, await fixture.Db.WorkOrderHistoryEvents.CountAsync());
    }

    [Fact]
    public async Task Missed_processing_catches_all_reached_levels_with_active_role_routing_only()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture, -5);
        var manager = await fixture.SeedUserAsync("MANAGER");
        var multiRoleManager = await fixture.SeedUserAsync("MANAGER", "ADMIN", "SUPERVISOR");
        var inactive = await fixture.SeedUserAsync("MANAGER");
        inactive.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.Reminders.ProcessDueAsync();
        Assert.Equal(0, result.Errors);
        var notifications = await fixture.Db.Notifications.AsNoTracking().ToListAsync();
        Assert.Equal(new[] { data.Technician.Id, data.Supervisor.Id }.Order(),
            notifications.Where(x => x.NotificationType == NotificationType.WORK_ORDER_OVERDUE).Select(x => x.UserId).Order());
        Assert.Equal(data.Supervisor.Id, Assert.Single(notifications, x => x.NotificationType == NotificationType.ESCALATION_SUPERVISOR).UserId);
        Assert.Equal(new[] { manager.Id, multiRoleManager.Id }.Order(),
            notifications.Where(x => x.NotificationType == NotificationType.ESCALATION_MANAGER).Select(x => x.UserId).Order());
        Assert.DoesNotContain(notifications, x => x.UserId == inactive.Id || x.UserId == data.Administrator.Id);
        Assert.DoesNotContain(notifications, x => x.NotificationType is NotificationType.WORK_ORDER_DUE or NotificationType.WORK_ORDER_DUE_SOON);
        Assert.Equal(5, await fixture.Db.WorkOrderEscalations.CountAsync());
    }

    [Fact]
    public async Task The_same_technician_and_supervisor_receive_one_copy_of_the_shared_overdue_event()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture, -1);
        var user = await fixture.Db.Users.Include(x => x.UserRoles).SingleAsync(x => x.Id == data.Technician.Id);
        var role = await fixture.Db.Roles.SingleAsync(x => x.Name == "SUPERVISOR");
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });
        var order = await fixture.Db.WorkOrders.SingleAsync();
        order.SupervisorId = user.Id;
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.Reminders.ProcessDueAsync();
        Assert.Equal(0, result.Errors);
        var overdue = Assert.Single(await fixture.Db.Notifications.Where(x => x.NotificationType == NotificationType.WORK_ORDER_OVERDUE).ToListAsync());
        Assert.Equal(user.Id, overdue.UserId);
        Assert.Equal(1, await fixture.Db.WorkOrderEscalations.CountAsync());
    }

    [Fact]
    public async Task Missing_technician_routes_action_to_the_assigned_supervisor()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture, -1, assignTechnician: false);
        var result = await fixture.Reminders.ProcessDueAsync();
        Assert.Equal(0, result.Errors);
        var overdue = Assert.Single(await fixture.Db.Notifications.Where(x => x.NotificationType == NotificationType.WORK_ORDER_OVERDUE).ToListAsync());
        Assert.Equal(data.Supervisor.Id, overdue.UserId);
        var escalation = Assert.Single(await fixture.Db.WorkOrderEscalations.ToListAsync());
        Assert.False(string.IsNullOrWhiteSpace(escalation.RoutingReason));
        Assert.Equal(WorkOrderLifecycleStatus.PLANNED, (await fixture.Db.WorkOrders.SingleAsync()).LifecycleStatus);
    }

    [Fact]
    public async Task Inactive_supervisor_falls_back_to_managers_and_records_why()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture, -3);
        var manager = await fixture.SeedUserAsync("MANAGER");
        var supervisor = await fixture.Db.Users.SingleAsync(x => x.Id == data.Supervisor.Id);
        supervisor.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        Assert.Equal(0, (await fixture.Reminders.ProcessDueAsync()).Errors);
        var escalations = await fixture.Db.WorkOrderEscalations.ToListAsync();
        Assert.Equal(manager.Id, Assert.Single(escalations, x => x.Level == 2).RecipientUserId);
        Assert.DoesNotContain(escalations, x => x.RecipientUserId == data.Supervisor.Id);
        Assert.All(escalations.Where(x => x.RecipientUserId == manager.Id), x => Assert.False(string.IsNullOrWhiteSpace(x.RoutingReason)));
    }

    [Fact]
    public async Task Unroutable_events_survive_and_retry_once_a_manager_exists()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture, -5);
        var supervisor = await fixture.Db.Users.SingleAsync(x => x.Id == data.Supervisor.Id);
        supervisor.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        var first = await fixture.Reminders.ProcessDueAsync();
        Assert.True(first.Errors > 0);
        var pending = await fixture.Db.NotificationEvents.Where(x => x.ProcessedAt == null).ToListAsync();
        Assert.NotEmpty(pending);
        Assert.All(pending, x => Assert.False(string.IsNullOrWhiteSpace(x.LastError)));
        var alreadyDelivered = await fixture.Db.Notifications.CountAsync(x => x.NotificationType == NotificationType.WORK_ORDER_OVERDUE && x.UserId == data.Technician.Id);
        Assert.Equal(1, alreadyDelivered);
        var manager = await fixture.SeedUserAsync("MANAGER");
        var retried = await fixture.Reminders.ProcessDueAsync();
        Assert.Equal(0, retried.Errors);
        Assert.Empty(await fixture.Db.NotificationEvents.Where(x => x.ProcessedAt == null).ToListAsync());
        Assert.Contains(await fixture.Db.Notifications.ToListAsync(), x => x.UserId == manager.Id && x.NotificationType == NotificationType.ESCALATION_MANAGER);
        Assert.Equal(1, await fixture.Db.Notifications.CountAsync(x => x.NotificationType == NotificationType.WORK_ORDER_OVERDUE && x.UserId == data.Technician.Id));
        Assert.Equal(0, (await fixture.Reminders.ProcessDueAsync()).NotificationsCreated);
    }

    [Fact]
    public async Task Submission_resolves_escalations_and_rejection_resume_never_restarts_technician_reminders()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture, -5);
        await fixture.SeedUserAsync("MANAGER");
        await fixture.Reminders.ProcessDueAsync();
        Assert.NotEmpty(await fixture.Db.WorkOrderEscalations.Where(x => x.ResolvedAt == null).ToListAsync());
        var submission = await data.SubmitAsync(fixture);
        Assert.Empty(await fixture.Db.WorkOrderEscalations.Where(x => x.ResolvedAt == null).ToListAsync());
        fixture.ActAs(data.Supervisor);
        await fixture.Reviews.RejectAsync(data.WorkOrder.Id, new(submission.Submission.Id, "Correct the observation"));
        fixture.ActAs(data.Technician);
        await fixture.Execution.ResumeAsync(data.WorkOrder.Id);
        var remindersBefore = await fixture.Db.Notifications.CountAsync(x => x.NotificationType == NotificationType.WORK_ORDER_OVERDUE ||
            x.NotificationType == NotificationType.ESCALATION_SUPERVISOR || x.NotificationType == NotificationType.ESCALATION_MANAGER);
        fixture.Clock.Advance(TimeSpan.FromDays(10));
        fixture.ActAs(data.Administrator);
        var result = await fixture.Reminders.ProcessDueAsync();
        Assert.Equal(0, result.NotificationsCreated);
        Assert.Equal(0, result.EscalationsCreated);
        Assert.Equal(remindersBefore, await fixture.Db.Notifications.CountAsync(x => x.NotificationType == NotificationType.WORK_ORDER_OVERDUE ||
            x.NotificationType == NotificationType.ESCALATION_SUPERVISOR || x.NotificationType == NotificationType.ESCALATION_MANAGER));
        var order = await fixture.Db.WorkOrders.SingleAsync();
        Assert.Equal(WorkOrderLifecycleStatus.IN_PROGRESS, order.LifecycleStatus);
        Assert.NotNull(order.SubmittedAt);
        Assert.Equal(0, order.EscalationLevel);
        Assert.All(await fixture.Db.WorkOrderEscalations.ToListAsync(), x => Assert.NotNull(x.ResolvedAt));
    }

    [Fact]
    public async Task Escalation_queries_scope_supervisors_and_filter_before_paging()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture, -5);
        var manager = await fixture.SeedUserAsync("MANAGER");
        await fixture.Reminders.ProcessDueAsync();
        fixture.ActAs(data.Supervisor);
        var all = await fixture.Escalations.ListAsync(new());
        Assert.Equal(4, all.TotalCount);
        Assert.Equal(4, (await fixture.Escalations.ListAsync(new(WorkOrderId: data.WorkOrder.Id,
            TechnicianId: data.Technician.Id, SupervisorId: data.Supervisor.Id, UnresolvedOnly: true))).TotalCount);
        var level3 = Assert.Single((await fixture.Escalations.ListAsync(new(Level: 3))).Items);
        Assert.Equal(manager.Id, level3.RecipientUserId);
        Assert.Equal(level3.Id, (await fixture.Escalations.GetAsync(level3.Id)).Id);
        var page = await fixture.Escalations.ListAsync(new(Page: 2, PageSize: 1));
        Assert.Single(page.Items);
        Assert.Equal(4, page.TotalCount);
        fixture.ActAs(await fixture.SeedUserAsync("SUPERVISOR"));
        Assert.Empty((await fixture.Escalations.ListAsync(new())).Items);
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Escalations.GetAsync(level3.Id));
        fixture.ActAs(data.Technician);
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.Escalations.ListAsync(new()));
        fixture.ActAs(manager);
        Assert.Equal(4, (await fixture.Escalations.ListAsync(new())).TotalCount);
        fixture.ActAs(data.Administrator);
        Assert.Equal(4, (await fixture.Escalations.ListAsync(new())).TotalCount);
    }

    [Fact]
    public async Task Manual_processing_rejects_technicians_and_supervisors()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture);
        foreach (var user in new[] { data.Technician, data.Supervisor })
        {
            fixture.ActAs(user);
            await ModuleFixture.ExpectStatusAsync(403, () => fixture.Reminders.ProcessDueAsync());
        }
    }
}
