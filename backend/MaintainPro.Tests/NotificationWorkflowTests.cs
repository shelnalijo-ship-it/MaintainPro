using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Execution;
using MaintainPro.Application.Reviews;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class NotificationWorkflowTests
{
    [Fact]
    public async Task Assignment_submission_rejection_resubmission_and_approval_emit_exact_recipient_events()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture);
        var assigned = Assert.Single(await fixture.Db.Notifications.Where(x => x.NotificationType == NotificationType.WORK_ORDER_ASSIGNED).ToListAsync());
        Assert.Equal(data.Technician.Id, assigned.UserId);
        Assert.Equal(data.WorkOrder.Id, assigned.EntityId);
        var first = await data.SubmitAsync(fixture);
        var submitted = Assert.Single(await fixture.Db.Notifications.Where(x => x.NotificationType == NotificationType.WORK_ORDER_SUBMITTED).ToListAsync());
        Assert.Equal(data.Supervisor.Id, submitted.UserId);
        fixture.ActAs(data.Supervisor);
        await fixture.Reviews.RejectAsync(data.WorkOrder.Id, new(first.Submission.Id, "Please clarify the reading"));
        Assert.Equal(data.Technician.Id, Assert.Single(await fixture.Db.Notifications.Where(x => x.NotificationType == NotificationType.WORK_ORDER_REJECTED).ToListAsync()).UserId);
        fixture.ActAs(data.Technician);
        await fixture.Execution.ResumeAsync(data.WorkOrder.Id);
        await fixture.Execution.SaveCommentsAsync(data.WorkOrder.Id, new("Corrected observation"));
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        var second = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        fixture.ActAs(data.Supervisor);
        await fixture.Reviews.ApproveAsync(data.WorkOrder.Id, new(second.Submission.Id));
        Assert.Equal(data.Technician.Id, Assert.Single(await fixture.Db.Notifications.Where(x => x.NotificationType == NotificationType.WORK_ORDER_APPROVED).ToListAsync()).UserId);
        Assert.Equal(2, await fixture.Db.Notifications.CountAsync(x => x.NotificationType == NotificationType.WORK_ORDER_SUBMITTED));
        Assert.Equal(5, await fixture.Db.Notifications.CountAsync());
        Assert.Equal(15, await fixture.Db.NotificationDeliveryAttempts.CountAsync());
        var keys = await fixture.Db.Notifications.Select(x => x.DeduplicationKey).ToListAsync();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        await fixture.AssertAuditHasNoSecretsAsync(fixture.Password);
    }

    [Fact]
    public async Task Reassignment_updates_current_assignment_and_emits_once_before_execution_starts()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture);
        var newTechnician = await fixture.SeedUserAsync("TECHNICIAN");
        fixture.ActAs(data.Administrator);
        var service = new WorkOrderAssignmentService(fixture.Db, fixture.Actor, fixture.Audit, fixture.Clock);
        await service.ChangeAsync(data.WorkOrder.Id, new WorkOrderAssignmentRequest(newTechnician.Id, "Covering the shift"));
        var reassigned = Assert.Single(await fixture.Db.Notifications.Where(x => x.NotificationType == NotificationType.WORK_ORDER_REASSIGNED).ToListAsync());
        Assert.Equal(newTechnician.Id, reassigned.UserId);
        await service.ChangeAsync(data.WorkOrder.Id, new WorkOrderAssignmentRequest(newTechnician.Id, "Same assignment retry"));
        Assert.Equal(1, await fixture.Db.Notifications.CountAsync(x => x.NotificationType == NotificationType.WORK_ORDER_REASSIGNED));
        fixture.ActAs(newTechnician);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        fixture.ActAs(data.Administrator);
        await ModuleFixture.ExpectStatusAsync(409, () => service.ChangeAsync(data.WorkOrder.Id, new WorkOrderAssignmentRequest(data.Technician.Id)));
    }

    [Fact]
    public async Task Failed_submission_audit_cannot_leave_notifications_or_pending_events()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        await fixture.Execution.SaveChecklistResultsAsync(data.WorkOrder.Id,
            new([new ChecklistResultRequest(Assert.Single(data.WorkOrder.Definition.Items).Id, ConfirmationValue: true)]));
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        var beforeNotifications = await fixture.Db.Notifications.CountAsync();
        var beforeEvents = await fixture.Db.NotificationEvents.CountAsync();
        var beforeAttempts = await fixture.Db.NotificationDeliveryAttempts.CountAsync();
        var failing = new WorkOrderSubmissionService(fixture.Db, fixture.Actor, new FailingAudit(), fixture.Clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.SubmitAsync(data.WorkOrder.Id));
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(beforeNotifications, await fixture.Db.Notifications.CountAsync());
        Assert.Equal(beforeEvents, await fixture.Db.NotificationEvents.CountAsync());
        Assert.Equal(beforeAttempts, await fixture.Db.NotificationDeliveryAttempts.CountAsync());
        Assert.Equal(WorkOrderLifecycleStatus.IN_PROGRESS, (await fixture.Db.WorkOrders.SingleAsync()).LifecycleStatus);
    }

    [Fact]
    public async Task Inactive_supervisor_routes_submission_to_active_managers_without_blocking_workflow()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await NotificationTestData.CreateAsync(fixture);
        var manager = await fixture.SeedUserAsync("MANAGER");
        var supervisor = await fixture.Db.Users.SingleAsync(x => x.Id == data.Supervisor.Id);
        supervisor.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        await data.SubmitAsync(fixture);
        var notification = Assert.Single(await fixture.Db.Notifications.Where(x => x.NotificationType == NotificationType.WORK_ORDER_SUBMITTED).ToListAsync());
        Assert.Equal(manager.Id, notification.UserId);
        Assert.Equal(WorkOrderLifecycleStatus.AWAITING_APPROVAL, (await fixture.Db.WorkOrders.SingleAsync()).LifecycleStatus);
    }

    private sealed class FailingAudit : IAuditWriter
    {
        public void Record(string action, string entityType, Guid? entityId, object? oldValues = null,
            object? newValues = null) => throw new InvalidOperationException("Simulated workflow audit failure.");
    }
}
