using System.Text.Json;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Planning;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class WorkOrderGenerationTests
{
    [Fact]
    public async Task Due_occurrence_creates_an_assigned_job_with_complete_immutable_definition_and_audit()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var source = await data.CreatePlanAsync(fixture);

        var result = await fixture.Generation.GenerateDueAsync();
        var job = await fixture.Db.WorkOrders.SingleAsync();
        var detail = await fixture.WorkOrders.GetAsync(job.Id);

        Assert.Equal(1, result.WorkOrdersCreated);
        Assert.Equal(0, result.Errors);
        Assert.False(result.HasMore);
        Assert.Equal(source.Plan.StartDate, job.PlannedDate);
        Assert.Equal(job.PlannedDate, job.DueDate);
        Assert.Equal(data.Technician.Id, job.AssignedTechnicianId);
        Assert.Equal(data.Supervisor.Id, job.SupervisorId);
        Assert.Equal(WorkOrderLifecycleStatus.ASSIGNED, job.LifecycleStatus);
        Assert.Equal($"WO-{job.PlannedDate.Year:D4}-0001", job.WorkOrderNumber);
        Assert.Equal(job.PlannedDate.AddDays(1), (await fixture.Db.MaintenancePlans.SingleAsync()).NextDueDate);
        var snapshot = detail.Definition;
        Assert.Equal(source.Plan.PlanName, snapshot.PlanName);
        Assert.Equal(data.Type.Name, snapshot.MaintenanceTypeName);
        Assert.Equal(data.Type.Id, snapshot.MaintenanceTypeId);
        Assert.Equal(source.Plan.Instructions, snapshot.Instructions);
        Assert.Equal(source.Plan.EstimatedDurationMinutes, snapshot.EstimatedDurationMinutes);
        Assert.Equal(source.Plan.PhotoRequired, snapshot.PhotoRequired);
        Assert.Equal(source.Plan.MinimumPhotoCount, snapshot.MinimumPhotoCount);
        Assert.Equal(source.Plan.CommentRequired, snapshot.CommentRequired);
        Assert.Equal(source.Plan.Priority, snapshot.Priority);
        Assert.Equal(data.Machine.MachineCode, snapshot.MachineCode);
        Assert.Equal(data.Machine.Name, snapshot.MachineName);
        Assert.Equal(data.Technician.EmployeeId, snapshot.AssignedTechnicianEmployeeId);
        Assert.Equal($"{data.Technician.FirstName} {data.Technician.LastName}", snapshot.AssignedTechnicianName);
        Assert.Equal(data.Supervisor.EmployeeId, snapshot.SupervisorEmployeeId);
        Assert.Equal(source.Checklist.Id, snapshot.ChecklistTemplateId);
        Assert.Equal(source.Checklist.Name, snapshot.ChecklistName);
        Assert.Equal(source.Checklist.Version, snapshot.ChecklistVersion);
        Assert.Equal(source.Checklist.Items.Count, snapshot.Items.Count);
        foreach (var item in source.Checklist.Items)
        {
            var copy = snapshot.Items.Single(copy => copy.SequenceNumber == item.SequenceNumber);
            Assert.Equal(item.Title, copy.Title);
            Assert.Equal(item.Description, copy.Description);
            Assert.Equal(item.ResponseType, copy.ResponseType);
            Assert.Equal(item.IsMandatory, copy.IsMandatory);
            Assert.Equal(item.Unit, copy.Unit);
            Assert.Equal(item.MinimumValue, copy.MinimumValue);
            Assert.Equal(item.MaximumValue, copy.MaximumValue);
            Assert.Equal(item.PhotoRequired, copy.PhotoRequired);
        }
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), log => log.Action == "WorkOrder.Generated"
            && log.EntityId == job.Id.ToString());
    }

    [Theory]
    [InlineData("future")]
    [InlineData("inactive-plan")]
    [InlineData("inactive-machine")]
    [InlineData("decommissioned-machine")]
    public async Task Future_or_inactive_schedules_do_not_generate_jobs(string condition)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        if (condition == "future") data = data with { Request = data.Request with { StartDate = data.Request.StartDate.AddDays(1) } };
        var source = await data.CreatePlanAsync(fixture);
        if (condition == "inactive-plan") await fixture.Plans.SetStatusAsync(source.Plan.Id, new PlanStatusRequest(false));
        if (condition == "inactive-machine") data.Machine.IsActive = false;
        if (condition == "decommissioned-machine") data.Machine.Status = MachineStatus.Decommissioned;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Generation.GenerateDueAsync();

        Assert.Equal(0, result.WorkOrdersCreated);
        Assert.Empty(await fixture.Db.WorkOrders.ToListAsync());
        Assert.Empty(await fixture.Db.WorkOrderNumberSequences.ToListAsync());
        Assert.Equal(source.Plan.NextDueDate, (await fixture.Db.MaintenancePlans.SingleAsync()).NextDueDate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_inactive_or_no_longer_qualified_technician_generates_unassigned_work(bool removeRole)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        await data.CreatePlanAsync(fixture);
        if (removeRole) fixture.Db.UserRoles.RemoveRange(await fixture.Db.UserRoles.Where(role => role.UserId == data.Technician.Id).ToListAsync());
        else data.Technician.IsActive = false;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Generation.GenerateDueAsync();
        var order = await fixture.Db.WorkOrders.Include(order => order.Definition).SingleAsync();

        Assert.Equal(1, result.WorkOrdersCreated);
        Assert.Null(order.AssignedTechnicianId);
        Assert.Null(order.Definition.AssignedTechnicianEmployeeId);
        Assert.Null(order.Definition.AssignedTechnicianName);
        Assert.Equal(WorkOrderLifecycleStatus.PLANNED, order.LifecycleStatus);
    }

    [Theory]
    [InlineData("supervisor")]
    [InlineData("type")]
    [InlineData("checklist")]
    public async Task Invalid_operational_definition_retains_due_cursor_and_reports_a_safe_error(string condition)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var plan = await fixture.Plans.CreateAsync(data.Request);
        if (condition != "checklist") await fixture.Plans.SetChecklistAsync(plan.Id, PlanningTestData.Checklist());
        if (condition == "supervisor") data.Supervisor.IsActive = false;
        if (condition == "type") (await fixture.Db.MaintenanceTypes.SingleAsync()).IsActive = false;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Generation.GenerateDueAsync();

        Assert.Equal(1, result.Errors);
        Assert.Equal(0, result.WorkOrdersCreated);
        Assert.Equal(plan.Id, Assert.Single(result.Issues).MaintenancePlanId);
        Assert.Equal(plan.NextDueDate, (await fixture.Db.MaintenancePlans.SingleAsync()).NextDueDate);
        Assert.Empty(await fixture.Db.WorkOrders.ToListAsync());
    }

    [Fact]
    public async Task Catch_up_is_bounded_resumable_and_durable_even_when_cursor_reverts()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var today = DateOnly.FromDateTime(fixture.Clock.GetUtcNow().UtcDateTime);
        var data = await PlanningTestData.CreateAsync(fixture, today.AddDays(-4));
        await data.CreatePlanAsync(fixture);

        var first = await fixture.Generation.GenerateDueAsync(new GenerationRequest { MaxOccurrences = 2 });
        var second = await fixture.Generation.GenerateDueAsync(new GenerationRequest { MaxOccurrences = 2 });
        var third = await fixture.Generation.GenerateDueAsync(new GenerationRequest { MaxOccurrences = 2 });
        var repeated = await fixture.Generation.GenerateDueAsync();

        Assert.Equal(2, first.WorkOrdersCreated);
        Assert.True(first.HasMore);
        Assert.Equal(2, second.WorkOrdersCreated);
        Assert.True(second.HasMore);
        Assert.Equal(1, third.WorkOrdersCreated);
        Assert.False(third.HasMore);
        Assert.Equal(0, repeated.WorkOrdersCreated);
        Assert.Equal(Enumerable.Range(0, 5).Select(offset => today.AddDays(offset - 4)),
            await fixture.Db.WorkOrders.OrderBy(order => order.PlannedDate).Select(order => order.PlannedDate).ToListAsync());
        var plan = await fixture.Db.MaintenancePlans.SingleAsync();
        plan.NextDueDate = data.Request.StartDate;
        await fixture.Db.SaveChangesAsync();

        var recovered = await fixture.Generation.GenerateDueAsync();

        Assert.Equal(0, recovered.WorkOrdersCreated);
        Assert.Equal(5, recovered.Skipped);
        Assert.Equal(5, await fixture.Db.WorkOrders.CountAsync());
        Assert.Equal(5, (await fixture.Db.WorkOrderNumberSequences.SingleAsync()).LastValue);
        Assert.Equal(today.AddDays(1), (await fixture.Db.MaintenancePlans.SingleAsync()).NextDueDate);
    }

    [Fact]
    public async Task Changes_to_live_plan_checklist_machine_and_people_only_affect_future_jobs()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var source = await data.CreatePlanAsync(fixture);
        await fixture.Generation.GenerateDueAsync();
        var originalId = await fixture.Db.WorkOrders.Select(order => order.Id).SingleAsync();
        var original = await fixture.WorkOrders.GetAsync(originalId);
        var originalJson = JsonSerializer.Serialize(original.Definition);
        var newTechnician = await fixture.SeedUserAsync("TECHNICIAN");
        var newSupervisor = await fixture.SeedUserAsync("SUPERVISOR");
        await fixture.Plans.UpdateAsync(source.Plan.Id, data.Request with
        {
            PlanName = "Revised plan", Instructions = "Revised instructions", Priority = MaintenancePriority.LOW,
            DefaultTechnicianId = newTechnician.Id, SupervisorId = newSupervisor.Id
        });
        await fixture.Plans.SetChecklistAsync(source.Plan.Id, new ChecklistWriteRequest("Revised checklist", new[]
        {
            new ChecklistItemWriteRequest(1, "New check", ChecklistResponseType.CONFIRMATION)
        }));
        var machine = await fixture.Db.Machines.SingleAsync();
        machine.MachineCode = "RENAMED-MACHINE";
        machine.Name = "Renamed equipment";
        machine.MachineOwnerUserId = newTechnician.Id;
        (await fixture.Db.Users.SingleAsync(user => user.Id == data.Technician.Id)).FirstName = "Renamed technician";
        (await fixture.Db.Users.SingleAsync(user => user.Id == data.Supervisor.Id)).FirstName = "Renamed supervisor";
        (await fixture.Db.MaintenanceTypes.SingleAsync()).Name = "Renamed type";
        await fixture.Db.SaveChangesAsync();
        fixture.Clock.Advance(TimeSpan.FromDays(1));

        Assert.Equal(1, (await fixture.Generation.GenerateDueAsync()).WorkOrdersCreated);
        var preserved = await fixture.WorkOrders.GetAsync(originalId);
        Assert.Equal(originalJson, JsonSerializer.Serialize(preserved.Definition));
        Assert.Equal(original.WorkOrder.AssignedTechnicianId, preserved.WorkOrder.AssignedTechnicianId);
        Assert.Equal(original.WorkOrder.SupervisorId, preserved.WorkOrder.SupervisorId);
        Assert.Equal(original.WorkOrder.PlannedDate, preserved.WorkOrder.PlannedDate);
        Assert.Equal(original.WorkOrder.Priority, preserved.WorkOrder.Priority);
        var future = await fixture.Db.WorkOrders.Include(order => order.Definition).SingleAsync(order => order.Id != originalId);
        Assert.Equal(newTechnician.Id, future.AssignedTechnicianId);
        Assert.Equal(newSupervisor.Id, future.SupervisorId);
        Assert.Equal("Revised plan", future.Definition.PlanName);
        Assert.Equal("Revised instructions", future.Definition.Instructions);
        Assert.Equal("RENAMED-MACHINE", future.Definition.MachineCode);
        Assert.Equal("Renamed type", future.Definition.MaintenanceTypeName);
        Assert.Equal(2, future.Definition.ChecklistVersion);
    }

    [Fact]
    public async Task Recurrence_edits_reanchor_future_schedule_without_rewriting_existing_occurrences()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        fixture.Clock.SetUtc(new DateTimeOffset(2026, 2, 10, 12, 0, 0, TimeSpan.Zero));
        var data = await PlanningTestData.CreateAsync(fixture);
        var source = await data.CreatePlanAsync(fixture);
        await fixture.Generation.GenerateDueAsync();

        var edited = await fixture.Plans.UpdateAsync(source.Plan.Id, data.Request with
        {
            StartDate = new DateOnly(2026, 1, 15), FrequencyType = MaintenanceFrequencyType.MONTHLY
        });

        Assert.Equal(new DateOnly(2026, 2, 15), edited.NextDueDate);
        Assert.Equal(new DateOnly(2026, 2, 10), (await fixture.Db.WorkOrders.SingleAsync()).PlannedDate);
        Assert.Equal(0, (await fixture.Generation.GenerateDueAsync()).WorkOrdersCreated);
    }

    [Fact]
    public async Task Failure_rolls_back_job_snapshot_number_cursor_and_audit_atomically()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var source = await data.CreatePlanAsync(fixture);
        var beforeAudits = await fixture.Db.AuditLogs.CountAsync();
        var secretMarker = ModuleFixture.NewPassword();
        var generation = new WorkOrderGenerationService(fixture.Db, fixture.Actor,
            new UnavailableAudit(secretMarker), fixture.Recurrence, fixture.Clock, fixture.Numbers,
            new SqliteGenerationConcurrency(fixture.Db));

        var result = await generation.GenerateDueAsync();

        Assert.Equal(1, result.Errors);
        Assert.DoesNotContain(secretMarker, JsonSerializer.Serialize(result));
        Assert.Empty(await fixture.Db.WorkOrders.ToListAsync());
        Assert.Empty(await fixture.Db.WorkOrderDefinitions.ToListAsync());
        Assert.Empty(await fixture.Db.WorkOrderChecklistItems.ToListAsync());
        Assert.Empty(await fixture.Db.WorkOrderNumberSequences.ToListAsync());
        Assert.Equal(source.Plan.NextDueDate, (await fixture.Db.MaintenancePlans.SingleAsync()).NextDueDate);
        Assert.Equal(beforeAudits, await fixture.Db.AuditLogs.CountAsync());
        Assert.Equal(1, (await fixture.Generation.GenerateDueAsync()).WorkOrdersCreated);
        Assert.EndsWith("-0001", (await fixture.Db.WorkOrders.SingleAsync()).WorkOrderNumber);
    }

    [Theory]
    [InlineData("TECHNICIAN")]
    [InlineData("SUPERVISOR")]
    public async Task Operational_roles_cannot_trigger_generation(string role)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        fixture.ActAs(await fixture.SeedUserAsync(role));

        await ModuleFixture.ExpectStatusAsync(403, () => fixture.Generation.GenerateDueAsync());
    }

    private sealed class UnavailableAudit(string marker) : IAuditWriter
    {
        public void Record(string action, string entityType, Guid? entityId, object? oldValues = null,
            object? newValues = null) => throw new InvalidOperationException(marker);
    }
}
