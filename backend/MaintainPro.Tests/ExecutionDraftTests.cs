using MaintainPro.Application.Execution;
using MaintainPro.Application.Planning;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class ExecutionDraftTests
{
    [Theory]
    [InlineData(WorkOrderLifecycleStatus.ASSIGNED)]
    [InlineData(WorkOrderLifecycleStatus.PLANNED)]
    public async Task Assigned_technician_starts_work_and_preserves_execution_identity(WorkOrderLifecycleStatus initial)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        data.WorkOrder.LifecycleStatus = initial;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Execution.StartAsync(data.WorkOrder.Id);

        Assert.Equal(WorkOrderLifecycleStatus.IN_PROGRESS, result.LifecycleStatus);
        Assert.Equal(fixture.Clock.GetUtcNow().UtcDateTime, result.StartedAt);
        Assert.Equal(data.Technician.Id, result.TechnicianId);
        Assert.Equal(data.Technician.EmployeeId, result.TechnicianEmployeeId);
        Assert.False(result.IsComplete);
        Assert.Null(result.CompletedAt);
        Assert.Null(result.ApprovedAt);
        Assert.Equal(0m, result.DurationMinutes);
    }

    [Theory]
    [InlineData(WorkOrderLifecycleStatus.CANCELLED)]
    [InlineData(WorkOrderLifecycleStatus.IN_PROGRESS)]
    [InlineData(WorkOrderLifecycleStatus.AWAITING_APPROVAL)]
    [InlineData(WorkOrderLifecycleStatus.APPROVED)]
    public async Task Start_rejects_invalid_lifecycle_transitions(WorkOrderLifecycleStatus initial)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        data.WorkOrder.LifecycleStatus = initial;
        await fixture.Db.SaveChangesAsync();

        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Execution.StartAsync(data.WorkOrder.Id));
    }

    [Theory]
    [InlineData("TECHNICIAN", 404)]
    [InlineData("SUPERVISOR", 404)]
    [InlineData("MANAGER", 403)]
    [InlineData("ADMIN", 403)]
    public async Task Starting_requires_the_assigned_technician_role_and_identity(string role, int status)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        var unrelated = await fixture.SeedUserAsync(role);
        fixture.ActAs(unrelated);

        await ModuleFixture.ExpectStatusAsync(status, () => fixture.Execution.StartAsync(data.WorkOrder.Id));
        Assert.Equal(WorkOrderLifecycleStatus.ASSIGNED, data.WorkOrder.LifecycleStatus);
    }

    [Fact]
    public async Task Execution_reads_follow_assignment_visibility_and_role_union()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        Assert.Equal(data.WorkOrder.Id, (await fixture.Execution.GetAsync(data.WorkOrder.Id)).WorkOrderId);
        fixture.ActAs(data.Supervisor);
        Assert.Equal(data.WorkOrder.Id, (await fixture.Execution.GetAsync(data.WorkOrder.Id)).WorkOrderId);
        fixture.ActAs(data.Administrator);
        Assert.Equal(data.WorkOrder.Id, (await fixture.Execution.GetAsync(data.WorkOrder.Id)).WorkOrderId);
        var manager = await fixture.SeedUserAsync("MANAGER");
        fixture.ActAs(manager);
        Assert.Equal(data.WorkOrder.Id, (await fixture.Execution.GetAsync(data.WorkOrder.Id)).WorkOrderId);
        var unrelated = await fixture.SeedUserAsync("TECHNICIAN", "SUPERVISOR");
        fixture.ActAs(unrelated);
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Execution.GetAsync(data.WorkOrder.Id));
        data.WorkOrder.SupervisorId = unrelated.Id;
        await fixture.Db.SaveChangesAsync();
        Assert.Equal(data.WorkOrder.Id, (await fixture.Execution.GetAsync(data.WorkOrder.Id)).WorkOrderId);
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.Execution.SaveCommentsAsync(data.WorkOrder.Id,
            new ExecutionCommentsRequest("Not the executing technician")));
    }

    [Theory]
    [InlineData(ChecklistResponseType.BOOLEAN)]
    [InlineData(ChecklistResponseType.PASS_FAIL)]
    [InlineData(ChecklistResponseType.NUMBER)]
    [InlineData(ChecklistResponseType.TEXT)]
    [InlineData(ChecklistResponseType.CONFIRMATION)]
    public async Task Checklist_preserves_the_correct_response_type(ChecklistResponseType type)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture,
            new ChecklistWriteRequest("Typed checklist", [new ChecklistItemWriteRequest(1, "Answer", type)]));
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        var answer = type switch
        {
            ChecklistResponseType.BOOLEAN => new ChecklistResultRequest(data.Item.Id, BooleanValue: false),
            ChecklistResponseType.PASS_FAIL => new ChecklistResultRequest(data.Item.Id, PassFailValue: PassFailResult.FAIL),
            ChecklistResponseType.NUMBER => new ChecklistResultRequest(data.Item.Id, NumericValue: 0m),
            ChecklistResponseType.TEXT => new ChecklistResultRequest(data.Item.Id, TextValue: "Observed stable operation"),
            _ => new ChecklistResultRequest(data.Item.Id, ConfirmationValue: true)
        };

        var result = await fixture.Execution.SaveChecklistResultsAsync(data.WorkOrder.Id, new ChecklistResultsRequest([answer]));

        var saved = Assert.Single(result.ChecklistResults);
        Assert.Equal(answer.BooleanValue, saved.BooleanValue);
        Assert.Equal(answer.NumericValue, saved.NumericValue);
        Assert.Equal(answer.TextValue, saved.TextValue);
        Assert.Equal(answer.PassFailValue, saved.PassFailValue);
        Assert.Equal(answer.ConfirmationValue, saved.ConfirmationValue);
        Assert.Equal(data.Technician.Id, saved.CompletedByUserId);
        Assert.NotNull(saved.CompletedAt);
    }

    [Theory]
    [InlineData(ChecklistResponseType.BOOLEAN)]
    [InlineData(ChecklistResponseType.PASS_FAIL)]
    [InlineData(ChecklistResponseType.NUMBER)]
    [InlineData(ChecklistResponseType.TEXT)]
    [InlineData(ChecklistResponseType.CONFIRMATION)]
    [InlineData(ChecklistResponseType.PHOTO)]
    public async Task Checklist_rejects_answers_from_an_incompatible_response_type(ChecklistResponseType type)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture,
            new ChecklistWriteRequest("Typed checklist", [new ChecklistItemWriteRequest(1, "Answer", type)]));
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        var answer = type == ChecklistResponseType.TEXT
            ? new ChecklistResultRequest(data.Item.Id, NumericValue: 1m)
            : new ChecklistResultRequest(data.Item.Id, TextValue: "Wrong type");

        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Execution.SaveChecklistResultsAsync(
            data.WorkOrder.Id, new ChecklistResultsRequest([answer])));
        Assert.Empty((await fixture.Execution.GetAsync(data.WorkOrder.Id)).ChecklistResults);
    }

    [Theory]
    [InlineData(-1, NumericReadingStatus.BELOW)]
    [InlineData(0, NumericReadingStatus.WITHIN)]
    [InlineData(5, NumericReadingStatus.WITHIN)]
    [InlineData(10, NumericReadingStatus.WITHIN)]
    [InlineData(11, NumericReadingStatus.ABOVE)]
    public async Task Numerical_readings_keep_the_value_and_report_inclusive_limit_warnings(int value, NumericReadingStatus expected)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture,
            new ChecklistWriteRequest("Pressure", [new ChecklistItemWriteRequest(1, "Reading", ChecklistResponseType.NUMBER,
                Unit: "bar", MinimumValue: 0m, MaximumValue: 10m)]));
        await fixture.Execution.StartAsync(data.WorkOrder.Id);

        var result = await fixture.Execution.SaveChecklistResultsAsync(data.WorkOrder.Id,
            new ChecklistResultsRequest([new ChecklistResultRequest(data.Item.Id, NumericValue: value)]));

        Assert.Equal(value, Assert.Single(result.ChecklistResults).NumericValue);
        Assert.Equal(expected, Assert.Single(result.ChecklistResults).ReadingStatus);
    }

    [Fact]
    public async Task Checklist_rejects_duplicate_foreign_and_null_items_atomically()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        var answer = new ChecklistResultRequest(data.Item.Id, ConfirmationValue: true);
        foreach (var invalid in new[]
        {
            new ChecklistResultsRequest([answer, answer]),
            new ChecklistResultsRequest([answer, new ChecklistResultRequest(Guid.NewGuid(), ConfirmationValue: true)]),
            new ChecklistResultsRequest([null!]),
            new ChecklistResultsRequest(null!)
        })
        {
            await ModuleFixture.ExpectStatusAsync(400, () => fixture.Execution.SaveChecklistResultsAsync(data.WorkOrder.Id, invalid));
            fixture.Db.ChangeTracker.Clear();
            Assert.Empty((await fixture.Execution.GetAsync(data.WorkOrder.Id)).ChecklistResults);
        }
    }

    [Theory]
    [InlineData(WorkOrderLifecycleStatus.ASSIGNED)]
    [InlineData(WorkOrderLifecycleStatus.REJECTED)]
    [InlineData(WorkOrderLifecycleStatus.AWAITING_APPROVAL)]
    [InlineData(WorkOrderLifecycleStatus.APPROVED)]
    [InlineData(WorkOrderLifecycleStatus.CANCELLED)]
    public async Task Draft_mutations_require_an_editable_job(WorkOrderLifecycleStatus state)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        if (state != WorkOrderLifecycleStatus.ASSIGNED)
        {
            data.WorkOrder.LifecycleStatus = state;
            await fixture.Db.SaveChangesAsync();
        }
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Execution.SaveCommentsAsync(data.WorkOrder.Id,
            new ExecutionCommentsRequest("Not editable")));
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Execution.AddPartAsync(data.WorkOrder.Id,
            new PartUsageRequest("Bearing", 1m)));
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Execution.AddDefectAsync(data.WorkOrder.Id,
            new DefectRequest("Leak", "Seal requires replacement")));
    }

    [Fact]
    public async Task Completion_and_further_edits_require_another_completion_and_accumulate_only_active_time()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        var started = await fixture.Execution.StartAsync(data.WorkOrder.Id);
        fixture.Clock.Advance(TimeSpan.FromMinutes(12));
        var first = await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        Assert.True(first.IsComplete);
        Assert.Equal(12m, first.DurationMinutes);
        Assert.Equal(fixture.Clock.GetUtcNow().UtcDateTime, first.CompletedAt);
        Assert.Null(first.ApprovedAt);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Execution.CompleteAsync(data.WorkOrder.Id));
        fixture.Clock.Advance(TimeSpan.FromHours(2));
        var edited = await fixture.Execution.SaveCommentsAsync(data.WorkOrder.Id, new ExecutionCommentsRequest("Added observation"));
        Assert.False(edited.IsComplete);
        Assert.Null(edited.CompletedAt);
        Assert.Equal(started.StartedAt, edited.StartedAt);
        fixture.Clock.Advance(TimeSpan.FromMinutes(3));
        var second = await fixture.Execution.CompleteAsync(data.WorkOrder.Id);
        Assert.Equal(15m, second.DurationMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Parts_require_positive_quantity(int quantity)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Execution.AddPartAsync(data.WorkOrder.Id,
            new PartUsageRequest("Bearing", quantity)));
    }

    [Fact]
    public async Task Parts_and_defects_support_draft_edits_and_removal_with_persisted_audits()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExecutionTestData.CreateAsync(fixture);
        await fixture.Execution.StartAsync(data.WorkOrder.Id);
        var part = await fixture.Execution.AddPartAsync(data.WorkOrder.Id, new PartUsageRequest("Bearing", 1.5m, "BR-1"));
        var defect = await fixture.Execution.AddDefectAsync(data.WorkOrder.Id, new DefectRequest("Leak", "Seal leaking", "HIGH", true));
        var changedPart = await fixture.Execution.UpdatePartAsync(data.WorkOrder.Id, part.Id, new PartUsageRequest("New bearing", 2m));
        var changedDefect = await fixture.Execution.UpdateDefectAsync(data.WorkOrder.Id, defect.Id,
            new DefectRequest("Small leak", "Inspected seal", "LOW", false));
        Assert.Equal(2m, changedPart.Quantity);
        Assert.Equal("Small leak", changedDefect.Title);
        await fixture.Execution.DeletePartAsync(data.WorkOrder.Id, part.Id);
        await fixture.Execution.DeleteDefectAsync(data.WorkOrder.Id, defect.Id);
        var execution = await fixture.Execution.GetAsync(data.WorkOrder.Id);
        Assert.Empty(execution.Parts);
        Assert.Empty(execution.Defects);
        var actions = await fixture.Db.AuditLogs.Where(x => x.EntityId == data.WorkOrder.Id.ToString()).Select(x => x.Action).ToListAsync();
        Assert.Contains(actions, action => action.Contains("part", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("defect", StringComparison.OrdinalIgnoreCase));
    }
}
