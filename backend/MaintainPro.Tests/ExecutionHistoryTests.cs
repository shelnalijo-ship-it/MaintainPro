using System.Text.Json;
using MaintainPro.Application.Execution;
using MaintainPro.Application.Planning;
using MaintainPro.Application.Reviews;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class ExecutionHistoryTests
{
    [Fact]
    public async Task Fixed_clock_history_retains_actual_workflow_order_and_exact_submission_versions()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        await fixture.Execution.SaveChecklistResultsAsync(data.WorkOrder.Id,
            new([new ChecklistResultRequest(data.Item.Id, ConfirmationValue: true)]));
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        var first = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        fixture.ActAs(data.Supervisor);
        await fixture.Reviews.RejectAsync(data.WorkOrder.Id, new(first.Submission.Id, "Correct observation"));
        fixture.ActAs(data.Technician);
        await fixture.Execution.ResumeAsync(data.WorkOrder.Id);
        await fixture.Execution.SaveCommentsAsync(data.WorkOrder.Id, new("Corrected observation"));
        await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        var second = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        fixture.ActAs(data.Supervisor);
        await fixture.Reviews.ApproveAsync(data.WorkOrder.Id, new(second.Submission.Id, "Reviewed"));

        var events = await fixture.History.HistoryAsync(data.WorkOrder.Id);

        Assert.Equal(new[]
        {
            "WorkOrder.Generated", "WorkOrder.Assigned", "WorkOrder.Started", "WorkOrder.Completed",
            "WorkOrder.Submitted", "WorkOrder.Rejected", "WorkOrder.Resumed", "WorkOrder.Completed",
            "WorkOrder.Submitted", "WorkOrder.Approved"
        }, events.Select(x => x.Action));
        Assert.Equal(Enumerable.Range(1, events.Count), events.Select(x => x.SequenceNumber));
        Assert.Single(events.Select(x => x.OccurredAt).Distinct());
        Assert.Equal(new int?[] { 1, 2 }, events.Where(x => x.Action == "WorkOrder.Submitted").Select(x => x.SubmissionVersion));
        Assert.Equal(first.Submission.Id, events.Single(x => x.Action == "WorkOrder.Rejected").WorkOrderSubmissionId);
        Assert.Equal(second.Submission.Id, events.Single(x => x.Action == "WorkOrder.Approved").WorkOrderSubmissionId);
        var audit = await fixture.Db.AuditLogs.Where(x => x.EntityId == data.WorkOrder.Id.ToString()).Select(x => x.Action).ToListAsync();
        foreach (var action in new[] { "WorkOrder.Started", "WorkOrder.Completed", "WorkOrder.Submitted", "WorkOrder.Rejected", "WorkOrder.Resumed", "WorkOrder.Approved" })
            Assert.Contains(action, audit);
        await fixture.AssertAuditHasNoSecretsAsync(fixture.Password, data.Technician.PasswordHash);
    }

    [Fact]
    public async Task Approved_machine_history_and_submissions_preserve_names_when_master_data_changes()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await ExecutionSubmissionTests.ReadyAsync(fixture, data);
        var originalTechnician = data.Technician.FirstName + " " + data.Technician.LastName;
        var submission = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        fixture.ActAs(data.Supervisor);
        await fixture.Reviews.ApproveAsync(data.WorkOrder.Id, new(submission.Submission.Id, "Accepted"));
        var historical = Assert.Single((await fixture.History.MachineHistoryAsync(data.Machine.Id, new())).Items);
        var frozen = JsonSerializer.Serialize(await fixture.Submissions.GetAsync(data.WorkOrder.Id, submission.Submission.Id));

        data.Technician.FirstName = "Renamed technician";
        data.Technician.EmployeeId = "RENAMED-EMP";
        data.Supervisor.FirstName = "Renamed supervisor";
        data.Machine.Name = "Renamed machine";
        data.Machine.MachineOwnerUserId = null;
        await fixture.Db.SaveChangesAsync();

        var history = Assert.Single((await fixture.History.MachineHistoryAsync(data.Machine.Id, new())).Items);
        Assert.Equal(historical, history);
        Assert.Equal(originalTechnician, history.TechnicianName);
        Assert.Equal(submission.Submission.Id, history.ApprovedSubmissionId);
        Assert.Equal(1, history.ApprovedSubmissionVersion);
        Assert.Equal(data.Plan.PlanName, history.PlanName);
        Assert.Equal(WorkOrderLifecycleStatus.APPROVED, history.LifecycleStatus);
        Assert.Equal(frozen, JsonSerializer.Serialize(await fixture.Submissions.GetAsync(data.WorkOrder.Id, submission.Submission.Id)));
        fixture.ActAs(data.Technician);
        Assert.Single((await fixture.History.MachineHistoryAsync(data.Machine.Id, new())).Items);
        fixture.ActAs(await fixture.SeedUserAsync("TECHNICIAN"));
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.History.MachineHistoryAsync(data.Machine.Id, new()));
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.History.HistoryAsync(data.WorkOrder.Id));
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Submissions.ListAsync(data.WorkOrder.Id));
    }

    [Fact]
    public async Task Pending_approval_filters_and_ordering_apply_priority_overdue_and_scope_before_pagination()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        var today = DateOnly.FromDateTime(fixture.Clock.GetUtcNow().UtcDateTime);
        var jobs = new List<WorkOrder> { data.WorkOrder };
        fixture.ActAs(data.Administrator);
        foreach (var priority in new[] { MaintenancePriority.CRITICAL, MaintenancePriority.HIGH })
        {
            var plan = await fixture.Plans.CreateAsync(new(data.Machine.Id, "Queue " + priority,
                data.Plan.MaintenanceTypeId, priority, MaintenanceFrequencyType.DAILY, 1,
                today, data.Supervisor.Id, data.Technician.Id));
            await fixture.Plans.SetChecklistAsync(plan.Id, new("Confirmation", [new(1, "Confirm", ChecklistResponseType.CONFIRMATION)]));
            Assert.Equal(1, (await fixture.Generation.GenerateDueAsync()).WorkOrdersCreated);
            jobs.Add(await fixture.Db.WorkOrders.Include(x => x.Definition).ThenInclude(x => x.Items).SingleAsync(x => x.MaintenancePlanId == plan.Id));
        }
        // Generation clears tracking between transactions, so reload the first job before
        // arranging its due dates; the earlier object may now be detached.
        var overdueJob = await fixture.Db.WorkOrders.SingleAsync(x => x.Id == jobs[0].Id);
        overdueJob.DueDate = today.AddDays(-1);
        overdueJob.PlannedDate = today.AddDays(-1);
        await fixture.Db.SaveChangesAsync();
        fixture.ActAs(data.Technician);
        foreach (var job in jobs)
        {
            await fixture.Execution.StartAsync(job.Id);
            await fixture.Execution.SaveChecklistResultsAsync(job.Id,
                new([new ChecklistResultRequest(Assert.Single(job.Definition.Items).Id, ConfirmationValue: true)]));
            await fixture.Execution.CompleteAsync(job.Id);
            await fixture.Submissions.SubmitAsync(job.Id);
            fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        }
        fixture.ActAs(data.Supervisor);
        var pending = await fixture.Reviews.PendingAsync(new());
        Assert.Equal(new[] { jobs[1].Id, jobs[0].Id, jobs[2].Id }, pending.Items.Select(x => x.WorkOrderId));
        Assert.Equal(jobs[0].Id, Assert.Single((await fixture.Reviews.PendingAsync(new(Overdue: true))).Items).WorkOrderId);
        Assert.Equal(jobs[1].Id, Assert.Single((await fixture.Reviews.PendingAsync(new(Priority: MaintenancePriority.CRITICAL))).Items).WorkOrderId);
        Assert.Equal(3, (await fixture.Reviews.PendingAsync(new(MachineId: data.Machine.Id, TechnicianId: data.Technician.Id,
            SubmittedFrom: today, SubmittedTo: today))).TotalCount);
        Assert.Empty((await fixture.Reviews.PendingAsync(new(TechnicianId: Guid.NewGuid()))).Items);
        var page = await fixture.Reviews.PendingAsync(new(Page: 2, PageSize: 1));
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(jobs[0].Id, Assert.Single(page.Items).WorkOrderId);
        fixture.ActAs(await fixture.SeedUserAsync("SUPERVISOR"));
        Assert.Equal(0, (await fixture.Reviews.PendingAsync(new())).TotalCount);
        fixture.ActAs(data.Administrator);
        Assert.Equal(3, (await fixture.Reviews.PendingAsync(new())).TotalCount);
        fixture.ActAs(data.Technician);
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.Reviews.PendingAsync(new()));
    }

    [Fact]
    public async Task Machine_history_includes_only_approved_work_and_pending_queue_removes_reviewed_versions()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        Assert.Empty((await fixture.History.MachineHistoryAsync(data.Machine.Id, new())).Items);
        await ExecutionSubmissionTests.ReadyAsync(fixture, data);
        var submission = await fixture.Submissions.SubmitAsync(data.WorkOrder.Id);
        fixture.ActAs(data.Supervisor);
        Assert.Single((await fixture.Reviews.PendingAsync(new())).Items);
        Assert.Empty((await fixture.History.MachineHistoryAsync(data.Machine.Id, new())).Items);
        await fixture.Reviews.ApproveAsync(data.WorkOrder.Id, new(submission.Submission.Id));
        Assert.Empty((await fixture.Reviews.PendingAsync(new())).Items);
        Assert.Single((await fixture.History.MachineHistoryAsync(data.Machine.Id, new())).Items);
    }
}
