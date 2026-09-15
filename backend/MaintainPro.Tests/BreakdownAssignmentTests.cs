using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class BreakdownAssignmentTests
{
    [Fact]
    public async Task Assignment_and_reassignment_append_identity_snapshots_and_notices_without_rewriting_prior_facts()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture, assigned: false);
        var replacement = await fixture.SeedUserAsync("TECHNICIAN");
        fixture.ActAs(data.Supervisor);
        var assigned = await fixture.Breakdowns.AssignAsync(data.Breakdown.Id, new(data.Technician.Id, Reason: "First shift"));
        Assert.Equal(BreakdownStatus.ASSIGNED, assigned.Status);
        await fixture.Breakdowns.AssignAsync(data.Breakdown.Id, new(replacement.Id, Reason: "Replacement shift"));
        var history = await fixture.Breakdowns.AssignmentsAsync(data.Breakdown.Id);
        Assert.Equal(new[] { 1, 2 }, history.Select(x => x.SequenceNumber));
        Assert.Equal(data.Technician.Id, history[0].TechnicianId);
        Assert.Equal(data.Technician.EmployeeId, history[0].TechnicianEmployeeId);
        Assert.Equal("First shift", history[0].Reason);
        Assert.Equal(replacement.Id, history[1].TechnicianId);
        Assert.Equal("Replacement shift", history[1].Reason);
        var notifications = await fixture.Db.Notifications.Where(x => x.NotificationType == NotificationType.BREAKDOWN_ASSIGNED).ToListAsync();
        Assert.Equal(new[] { data.Technician.Id, replacement.Id }.Order(), notifications.Select(x => x.UserId).Order());
        await fixture.Breakdowns.AssignAsync(data.Breakdown.Id, new(replacement.Id, Reason: "Retry same assignment"));
        Assert.Equal(2, (await fixture.Breakdowns.AssignmentsAsync(data.Breakdown.Id)).Count);
        Assert.Equal(2, await fixture.Db.Notifications.CountAsync(x => x.NotificationType == NotificationType.BREAKDOWN_ASSIGNED));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("role")]
    [InlineData("inactive")]
    public async Task Assignment_requires_an_existing_active_technician(string invalid)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture, assigned: false);
        var id = data.Technician.Id;
        if (invalid == "missing") id = Guid.NewGuid();
        if (invalid == "role") id = data.Supervisor.Id;
        if (invalid == "inactive")
        {
            (await fixture.Db.Users.SingleAsync(x => x.Id == id)).IsActive = false;
            await fixture.Db.SaveChangesAsync();
        }
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Breakdowns.AssignAsync(data.Breakdown.Id, new(id)));
        Assert.Empty(await fixture.Db.BreakdownAssignmentHistories.ToListAsync());
        Assert.Equal(BreakdownStatus.REPORTED, (await fixture.Breakdowns.GetAsync(data.Breakdown.Id)).Status);
    }

    [Fact]
    public async Task Assigned_supervisor_can_assign_but_only_manager_admin_can_change_supervisor()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture, assigned: false);
        var nextSupervisor = await fixture.SeedUserAsync("SUPERVISOR");
        fixture.ActAs(data.Supervisor);
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.Breakdowns.AssignAsync(data.Breakdown.Id, new(data.Technician.Id, nextSupervisor.Id)));
        fixture.ActAs(data.Administrator);
        var assigned = await fixture.Breakdowns.AssignAsync(data.Breakdown.Id, new(data.Technician.Id, nextSupervisor.Id));
        Assert.Equal(nextSupervisor.Id, assigned.SupervisorId);
        fixture.ActAs(data.Supervisor);
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Breakdowns.AssignAsync(data.Breakdown.Id, new(data.Technician.Id)));
    }

    [Fact]
    public async Task Assignment_is_forbidden_after_execution_starts_and_to_the_technician_even_before_start()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        fixture.ActAs(data.Technician);
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.Breakdowns.AssignAsync(data.Breakdown.Id, new(data.Technician.Id)));
        await fixture.CorrectiveExecution.StartAsync(data.Breakdown.Id);
        fixture.ActAs(data.Administrator);
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Breakdowns.AssignAsync(data.Breakdown.Id, new(data.Technician.Id)));
        Assert.Single(await fixture.Db.BreakdownAssignmentHistories.ToListAsync());
    }

    [Fact]
    public async Task Rejected_assignment_can_move_to_a_new_technician_without_changing_its_first_submission()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture);
        var first = await data.SubmitAsync(fixture);
        fixture.ActAs(data.Supervisor);
        await fixture.CorrectiveReviews.RejectAsync(data.Breakdown.Id, new(first.Submission.Id, "Further repair needed"));
        var replacement = await fixture.SeedUserAsync("TECHNICIAN");
        var reassigned = await fixture.Breakdowns.AssignAsync(data.Breakdown.Id, new(replacement.Id, Reason: "Specialist follow-up"));
        Assert.Equal(BreakdownStatus.REJECTED, reassigned.Status);
        Assert.Single(await fixture.Db.Notifications.Where(x => x.NotificationType == NotificationType.BREAKDOWN_ASSIGNED &&
            x.UserId == replacement.Id && x.EntityId == data.Breakdown.Id).ToListAsync());
        fixture.ActAs(replacement);
        await fixture.CorrectiveExecution.ResumeAsync(data.Breakdown.Id);
        await fixture.CorrectiveExecution.SaveAsync(data.Breakdown.Id, new("Misalignment", "Aligned and tested"));
        await fixture.CorrectiveExecution.CompleteAsync(data.Breakdown.Id);
        var second = await fixture.CorrectiveSubmissions.SubmitAsync(data.Breakdown.Id);
        Assert.Equal(replacement.Id, second.Submission.TechnicianId);
        Assert.Equal(2, second.Submission.VersionNumber);
        Assert.Equal(data.Technician.Id, (await fixture.CorrectiveSubmissions.GetAsync(data.Breakdown.Id, first.Submission.Id)).Submission.TechnicianId);
    }
}
