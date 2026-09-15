using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Audit;
using MaintainPro.Application.Breakdowns;
using MaintainPro.Domain.Enums;
using MaintainPro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class CorrectiveIntegrityTests
{
    [Fact]
    public async Task Stale_corrective_draft_cannot_overwrite_a_newer_draft_or_its_history()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);
        await fixture.CorrectiveExecution.StartAsync(data.Breakdown.Id);
        await using var stale = NewContext(fixture);
        await stale.Breakdowns.SingleAsync();
        await stale.CorrectiveActionDrafts.SingleAsync();
        var service = new CorrectiveExecutionService(stale, fixture.Actor,
            new AuditWriter(stale, fixture.Actor, fixture.Clock), fixture.Clock);
        await fixture.CorrectiveExecution.SaveAsync(data.Breakdown.Id, new("Winning cause", "Winning repair"));
        var auditCount = await fixture.Db.AuditLogs.CountAsync();
        var historyCount = await fixture.Db.BreakdownHistoryEvents.CountAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            service.SaveAsync(data.Breakdown.Id, new("Stale cause", "Stale repair")));

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal("Winning cause", (await fixture.Db.CorrectiveActionDrafts.SingleAsync()).RootCause);
        Assert.Equal(auditCount, await fixture.Db.AuditLogs.CountAsync());
        Assert.Equal(historyCount, await fixture.Db.BreakdownHistoryEvents.CountAsync());
    }

    [Fact]
    public async Task Stale_submission_cannot_create_a_second_version_or_duplicate_notifications()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);
        await fixture.CorrectiveExecution.StartAsync(data.Breakdown.Id);
        await fixture.CorrectiveExecution.SaveAsync(data.Breakdown.Id, new("Cause", "Repair"));
        await fixture.CorrectiveExecution.CompleteAsync(data.Breakdown.Id);
        await using var stale = NewContext(fixture);
        await stale.Breakdowns.SingleAsync();
        await stale.CorrectiveActionDrafts.SingleAsync();
        var service = new CorrectiveSubmissionService(stale, fixture.Actor,
            new AuditWriter(stale, fixture.Actor, fixture.Clock), fixture.Clock);
        var accepted = await fixture.CorrectiveSubmissions.SubmitAsync(data.Breakdown.Id);
        var auditCount = await fixture.Db.AuditLogs.CountAsync();
        var historyCount = await fixture.Db.BreakdownHistoryEvents.CountAsync();
        var notificationCount = await fixture.Db.Notifications.CountAsync();
        var eventCount = await fixture.Db.BreakdownNotificationEvents.CountAsync();

        var exception = await Record.ExceptionAsync(() => service.SubmitAsync(data.Breakdown.Id));

        Assert.IsAssignableFrom<DbUpdateException>(exception);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(accepted.Submission.Id, (await fixture.Db.CorrectiveSubmissions.SingleAsync()).Id);
        Assert.Equal(1, (await fixture.Db.Breakdowns.SingleAsync()).SubmissionVersion);
        Assert.Equal(auditCount, await fixture.Db.AuditLogs.CountAsync());
        Assert.Equal(historyCount, await fixture.Db.BreakdownHistoryEvents.CountAsync());
        Assert.Equal(notificationCount, await fixture.Db.Notifications.CountAsync());
        Assert.Equal(eventCount, await fixture.Db.BreakdownNotificationEvents.CountAsync());
    }

    [Fact]
    public async Task A_stale_supervisor_cannot_reject_an_approved_corrective_version()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        var submitted = await data.SubmitAsync(fixture);
        fixture.ActAs(data.Supervisor);
        await using var stale = NewContext(fixture);
        await stale.Breakdowns.SingleAsync();
        var service = new CorrectiveReviewService(stale, fixture.Actor,
            new AuditWriter(stale, fixture.Actor, fixture.Clock), fixture.Clock);
        await fixture.CorrectiveReviews.ApproveAsync(data.Breakdown.Id, new(submitted.Submission.Id, "Verified"));
        var historyCount = await fixture.Db.BreakdownHistoryEvents.CountAsync();
        var eventCount = await fixture.Db.BreakdownNotificationEvents.CountAsync();

        await ModuleFixture.ExpectStatusAsync(409, () =>
            service.RejectAsync(data.Breakdown.Id, new(submitted.Submission.Id, "Conflicting late rejection")));

        var approval = await fixture.Db.CorrectiveApprovals.AsNoTracking().SingleAsync();
        Assert.Equal(CorrectiveReviewDecision.APPROVED, approval.Decision);
        Assert.Equal("Verified", approval.Remarks);
        Assert.Equal(historyCount, await fixture.Db.BreakdownHistoryEvents.CountAsync());
        Assert.Equal(eventCount, await fixture.Db.BreakdownNotificationEvents.CountAsync());
    }

    [Fact]
    public async Task Closure_audit_failure_rolls_back_review_closure_history_and_notifications()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        var submitted = await data.SubmitAsync(fixture);
        fixture.ActAs(data.Supervisor);
        var historyCount = await fixture.Db.BreakdownHistoryEvents.CountAsync();
        var auditCount = await fixture.Db.AuditLogs.CountAsync();
        var notificationCount = await fixture.Db.Notifications.CountAsync();
        var eventCount = await fixture.Db.BreakdownNotificationEvents.CountAsync();
        var service = new CorrectiveReviewService(fixture.Db, fixture.Actor,
            new FailOnAction(fixture.Audit, "Breakdown.Closed"), fixture.Clock);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveAsync(data.Breakdown.Id, new(submitted.Submission.Id, "Verified")));

        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Db.CorrectiveApprovals.ToListAsync());
        var breakdown = await fixture.Db.Breakdowns.SingleAsync();
        Assert.Equal(BreakdownStatus.AWAITING_APPROVAL, breakdown.Status);
        Assert.Null(breakdown.ClosedAt);
        Assert.Equal(historyCount, await fixture.Db.BreakdownHistoryEvents.CountAsync());
        Assert.Equal(auditCount, await fixture.Db.AuditLogs.CountAsync());
        Assert.Equal(notificationCount, await fixture.Db.Notifications.CountAsync());
        Assert.Equal(eventCount, await fixture.Db.BreakdownNotificationEvents.CountAsync());
        await fixture.CorrectiveReviews.ApproveAsync(data.Breakdown.Id, new(submitted.Submission.Id));
        Assert.Equal(BreakdownStatus.CLOSED, (await fixture.Breakdowns.GetAsync(data.Breakdown.Id)).Status);
    }

    private static ApplicationDbContext NewContext(ModuleFixture fixture) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(fixture.Db.Database.GetDbConnection()).Options);

    private sealed class FailOnAction(IAuditWriter inner, string rejectedAction) : IAuditWriter
    {
        public void Record(string action, string entityType, Guid? entityId, object? oldValues = null, object? newValues = null)
        {
            if (action == rejectedAction) throw new InvalidOperationException("Simulated transactional audit failure.");
            inner.Record(action, entityType, entityId, oldValues, newValues);
        }
    }
}
