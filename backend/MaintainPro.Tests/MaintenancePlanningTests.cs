using MaintainPro.Application.Planning;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class MaintenancePlanningTests
{
    [Fact]
    public async Task Valid_plan_creation_preserves_scheduling_evidence_assignments_and_audit()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);

        var plan = await fixture.Plans.CreateAsync(data.Request);

        Assert.Equal(data.Machine.Id, plan.MachineId);
        Assert.Equal(data.Type.Id, plan.MaintenanceTypeId);
        Assert.Equal(data.Request.PlanName, plan.PlanName);
        Assert.Equal(data.Request.StartDate, plan.StartDate);
        Assert.Equal(plan.StartDate, plan.NextDueDate);
        Assert.Equal(data.Technician.Id, plan.DefaultTechnicianId);
        Assert.Equal(data.Supervisor.Id, plan.SupervisorId);
        Assert.Equal(data.Administrator.Id, plan.CreatedByUserId);
        Assert.Equal(MaintenancePriority.HIGH, plan.Priority);
        Assert.Equal(data.Request.Instructions, plan.Instructions);
        Assert.Equal(45, plan.EstimatedDurationMinutes);
        Assert.True(plan.PhotoRequired);
        Assert.Equal(2, plan.MinimumPhotoCount);
        Assert.True(plan.CommentRequired);
        Assert.Equal(DateTimeKind.Utc, plan.CreatedAt.Kind);
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), log => log.EntityType == nameof(MaintenancePlan)
            && log.EntityId == plan.Id.ToString() && log.Action.Contains("Created", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("TECHNICIAN", false)]
    [InlineData("SUPERVISOR", true)]
    [InlineData("MANAGER", true)]
    [InlineData("ADMIN", true)]
    public async Task Default_technician_requires_an_active_explicit_technician_role(string role, bool active)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var invalid = await fixture.SeedUserAsync(role);
        invalid.IsActive = active;
        await fixture.Db.SaveChangesAsync();

        await ModuleFixture.ExpectStatusAsync(400,
            () => fixture.Plans.CreateAsync(data.Request with { DefaultTechnicianId = invalid.Id }));
        Assert.Empty(await fixture.Db.MaintenancePlans.ToListAsync());
    }

    [Theory]
    [InlineData("SUPERVISOR", false)]
    [InlineData("TECHNICIAN", true)]
    [InlineData("ADMIN", true)]
    public async Task Supervisor_requires_an_active_explicit_supervisor_role(string role, bool active)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var invalid = await fixture.SeedUserAsync(role);
        invalid.IsActive = active;
        await fixture.Db.SaveChangesAsync();

        await ModuleFixture.ExpectStatusAsync(400,
            () => fixture.Plans.CreateAsync(data.Request with { SupervisorId = invalid.Id }));
    }

    [Theory]
    [InlineData("inactive-machine")]
    [InlineData("decommissioned-machine")]
    [InlineData("missing-machine")]
    [InlineData("inactive-type")]
    [InlineData("missing-type")]
    [InlineData("missing-technician")]
    [InlineData("missing-supervisor")]
    public async Task Plan_requires_valid_active_operational_references(string invalidReference)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var request = data.Request;
        switch (invalidReference)
        {
            case "inactive-machine": data.Machine.IsActive = false; break;
            case "decommissioned-machine": data.Machine.Status = MachineStatus.Decommissioned; break;
            case "missing-machine": request = request with { MachineId = Guid.NewGuid() }; break;
            case "inactive-type": (await fixture.Db.MaintenanceTypes.SingleAsync()).IsActive = false; break;
            case "missing-type": request = request with { MaintenanceTypeId = Guid.NewGuid() }; break;
            case "missing-technician": request = request with { DefaultTechnicianId = Guid.NewGuid() }; break;
            case "missing-supervisor": request = request with { SupervisorId = Guid.NewGuid() }; break;
        }
        await fixture.Db.SaveChangesAsync();

        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Plans.CreateAsync(request));
    }

    [Theory]
    [InlineData("frequency")]
    [InlineData("duration")]
    [InlineData("photos")]
    [InlineData("priority")]
    [InlineData("name")]
    public async Task Invalid_plan_values_are_rejected(string field)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var request = field switch
        {
            "frequency" => data.Request with { FrequencyValue = 0 },
            "duration" => data.Request with { EstimatedDurationMinutes = -1 },
            "photos" => data.Request with { MinimumPhotoCount = -1 },
            "priority" => data.Request with { Priority = (MaintenancePriority)999 },
            _ => data.Request with { PlanName = " " }
        };

        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Plans.CreateAsync(request));
    }

    [Fact]
    public async Task Plan_duplication_copies_definition_into_independent_first_checklist_version()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var original = await data.CreatePlanAsync(fixture);
        var nextStart = original.Plan.StartDate.AddDays(14);

        var duplicate = await fixture.Plans.DuplicateAsync(original.Plan.Id,
            new DuplicatePlanRequest("Another inspection", nextStart));
        var checklist = await fixture.Plans.GetChecklistAsync(duplicate.Id);

        Assert.NotEqual(original.Plan.Id, duplicate.Id);
        Assert.Equal("Another inspection", duplicate.PlanName);
        Assert.Equal(nextStart, duplicate.StartDate);
        Assert.Equal(nextStart, duplicate.NextDueDate);
        Assert.Equal(original.Plan.MachineId, duplicate.MachineId);
        Assert.Equal(original.Plan.DefaultTechnicianId, duplicate.DefaultTechnicianId);
        Assert.Equal(original.Plan.SupervisorId, duplicate.SupervisorId);
        Assert.Equal(original.Plan.Instructions, duplicate.Instructions);
        Assert.Equal(1, checklist.Version);
        Assert.NotEqual(original.Checklist.Id, checklist.Id);
        Assert.Equal(original.Checklist.Items.Select(item => item.Title), checklist.Items.Select(item => item.Title));
        Assert.Empty(original.Checklist.Items.Select(item => item.Id).Intersect(checklist.Items.Select(item => item.Id)));
    }

    [Fact]
    public async Task Deactivation_and_edit_preserve_plan_and_are_audited()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var plan = await fixture.Plans.CreateAsync(data.Request);

        var updated = await fixture.Plans.UpdateAsync(plan.Id, data.Request with { PlanName = "Updated inspection" });
        var disabled = await fixture.Plans.SetStatusAsync(plan.Id, new PlanStatusRequest(false));

        Assert.Equal("Updated inspection", updated.PlanName);
        Assert.False(disabled.IsActive);
        Assert.Equal(1, await fixture.Db.MaintenancePlans.CountAsync());
        Assert.Empty((await fixture.Plans.ListAsync(new PlanQuery(IsActive: true))).Items);
        Assert.Equal(plan.Id, Assert.Single((await fixture.Plans.ListAsync(new PlanQuery(IsActive: false))).Items).Id);
        var audits = await fixture.Db.AuditLogs.Where(log => log.EntityType == nameof(MaintenancePlan)).ToListAsync();
        Assert.Contains(audits, log => log.Action.Contains("Updated", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(audits, log => log.Action.Contains("Status", StringComparison.OrdinalIgnoreCase));
        fixture.Db.MaintenancePlans.Remove(await fixture.Db.MaintenancePlans.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("TECHNICIAN")]
    [InlineData("SUPERVISOR")]
    public async Task Operational_roles_cannot_manage_planning(string role)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        fixture.ActAs(await fixture.SeedUserAsync(role));

        await ModuleFixture.ExpectStatusAsync(403, () => fixture.Plans.CreateAsync(data.Request));
        await ModuleFixture.ExpectStatusAsync(403,
            () => fixture.MaintenanceTypes.CreateAsync(new MaintenanceTypeWriteRequest("Lubrication")));
    }

    [Fact]
    public async Task Maintenance_types_have_unique_normalized_names_and_soft_deactivation()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var type = await fixture.MaintenanceTypes.CreateAsync(new MaintenanceTypeWriteRequest(" Inspection "));
        await ModuleFixture.ExpectStatusAsync(409,
            () => fixture.MaintenanceTypes.CreateAsync(new MaintenanceTypeWriteRequest("inspection")));
        await fixture.MaintenanceTypes.UpdateAsync(type.Id, new MaintenanceTypeWriteRequest("Inspection", "Updated"));
        await fixture.MaintenanceTypes.SetStatusAsync(type.Id, new MaintenanceTypeStatusRequest(false));

        Assert.Empty(await fixture.MaintenanceTypes.ListAsync(new MaintenanceTypeQuery(IsActive: true)));
        Assert.False(Assert.Single(await fixture.MaintenanceTypes.ListAsync(new MaintenanceTypeQuery())).IsActive);
        Assert.Equal(1, await fixture.Db.MaintenanceTypes.CountAsync());
    }
}
