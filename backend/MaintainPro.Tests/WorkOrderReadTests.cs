using System.Text.Json;
using MaintainPro.Application.Planning;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class WorkOrderReadTests
{
    [Theory]
    [InlineData("TECHNICIAN", 1)]
    [InlineData("SUPERVISOR", 1)]
    [InlineData("TECHNICIAN|SUPERVISOR", 2)]
    [InlineData("MANAGER", 3)]
    [InlineData("ADMIN", 3)]
    [InlineData("", 0)]
    public async Task Work_order_and_calendar_visibility_use_assigned_jobs_and_role_union_before_paging(string roles, int expected)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var viewer = await fixture.SeedUserAsync("TECHNICIAN", "SUPERVISOR", "MANAGER", "ADMIN");
        var firstPlan = await fixture.Plans.CreateAsync(data.Request with { PlanName = "Assigned", DefaultTechnicianId = viewer.Id });
        var secondPlan = await fixture.Plans.CreateAsync(data.Request with { PlanName = "Supervised", SupervisorId = viewer.Id });
        var thirdPlan = await fixture.Plans.CreateAsync(data.Request with { PlanName = "Unrelated" });
        foreach (var plan in new[] { firstPlan, secondPlan, thirdPlan })
            await fixture.Plans.SetChecklistAsync(plan.Id, PlanningTestData.Checklist());
        Assert.Equal(3, (await fixture.Generation.GenerateDueAsync()).WorkOrdersCreated);
        var unrelatedId = await fixture.Db.WorkOrders.Where(order => order.MaintenancePlanId == thirdPlan.Id).Select(order => order.Id).SingleAsync();
        fixture.ActAs(viewer);
        fixture.Actor.Roles = roles.Split('|', StringSplitOptions.RemoveEmptyEntries);

        var page = await fixture.WorkOrders.ListAsync(new WorkOrderQuery(PageSize: 1));
        var all = await fixture.WorkOrders.ListAsync(new WorkOrderQuery());
        var calendar = await fixture.WorkOrders.CalendarAsync(new WorkOrderCalendarQuery(data.Request.StartDate, data.Request.StartDate));

        Assert.Equal(expected, page.TotalCount);
        Assert.Equal(Math.Min(expected, 1), page.Items.Count);
        Assert.Equal(expected, all.Items.Count);
        Assert.Equal(expected, calendar.Count);
        if (roles.Contains("TECHNICIAN")) Assert.Contains(all.Items, order => order.MaintenancePlanId == firstPlan.Id);
        if (roles.Contains("SUPERVISOR")) Assert.Contains(all.Items, order => order.MaintenancePlanId == secondPlan.Id);
        if (roles is "MANAGER" or "ADMIN") Assert.Equal(unrelatedId, (await fixture.WorkOrders.GetAsync(unrelatedId)).WorkOrder.Id);
        else
        {
            Assert.DoesNotContain(all.Items, order => order.Id == unrelatedId);
            await ModuleFixture.ExpectStatusAsync(404, () => fixture.WorkOrders.GetAsync(unrelatedId));
        }
    }

    [Fact]
    public async Task Work_order_filters_dates_search_and_pagination_each_select_correct_records()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var today = DateOnly.FromDateTime(fixture.Clock.GetUtcNow().UtcDateTime);
        var data = await PlanningTestData.CreateAsync(fixture, today.AddDays(-1));
        var otherTechnician = await fixture.SeedUserAsync("TECHNICIAN");
        var otherSupervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var otherMachine = await fixture.SeedMachineAsync(otherTechnician.Id, otherSupervisor.Id);
        var target = await fixture.Plans.CreateAsync(data.Request with
        {
            FrequencyType = MaintenanceFrequencyType.WEEKLY, Priority = MaintenancePriority.CRITICAL
        });
        var current = await fixture.Plans.CreateAsync(data.Request with
        {
            PlanName = "Current job", MachineId = otherMachine.Id, StartDate = today,
            DefaultTechnicianId = null, SupervisorId = otherSupervisor.Id, Priority = MaintenancePriority.LOW
        });
        var terminal = await fixture.Plans.CreateAsync(data.Request with
        {
            PlanName = "Historical completed job", MachineId = otherMachine.Id, StartDate = today.AddDays(-2),
            FrequencyType = MaintenanceFrequencyType.WEEKLY, DefaultTechnicianId = otherTechnician.Id,
            SupervisorId = otherSupervisor.Id, Priority = MaintenancePriority.LOW
        });
        foreach (var plan in new[] { target, current, terminal })
            await fixture.Plans.SetChecklistAsync(plan.Id, PlanningTestData.Checklist());
        Assert.Equal(3, (await fixture.Generation.GenerateDueAsync()).WorkOrdersCreated);
        // Seed a terminal historical state to verify read predicates; no execution endpoint is added.
        (await fixture.Db.WorkOrders.SingleAsync(order => order.MaintenancePlanId == terminal.Id)).LifecycleStatus = WorkOrderLifecycleStatus.APPROVED;
        await fixture.Db.SaveChangesAsync();
        var wanted = await fixture.Db.WorkOrders.AsNoTracking().SingleAsync(order => order.MaintenancePlanId == target.Id);
        var filters = new[]
        {
            new WorkOrderQuery(MachineId: data.Machine.Id),
            new WorkOrderQuery(MaintenancePlanId: target.Id),
            new WorkOrderQuery(TechnicianId: data.Technician.Id),
            new WorkOrderQuery(SupervisorId: data.Supervisor.Id),
            new WorkOrderQuery(LifecycleStatus: WorkOrderLifecycleStatus.ASSIGNED),
            new WorkOrderQuery(Priority: MaintenancePriority.CRITICAL),
            new WorkOrderQuery(PlannedFrom: today.AddDays(-1), PlannedTo: today.AddDays(-1)),
            new WorkOrderQuery(DueFrom: today.AddDays(-1), DueTo: today.AddDays(-1)),
            new WorkOrderQuery(Overdue: true),
            new WorkOrderQuery(Search: wanted.WorkOrderNumber),
            new WorkOrderQuery(Search: data.Machine.MachineCode.ToLowerInvariant()),
            new WorkOrderQuery(Search: data.Machine.Name.ToUpperInvariant()),
            new WorkOrderQuery(Search: target.PlanName.ToLowerInvariant())
        };
        foreach (var filter in filters)
            Assert.Equal(wanted.Id, Assert.Single((await fixture.WorkOrders.ListAsync(filter)).Items).Id);
        Assert.Equal(2, (await fixture.WorkOrders.ListAsync(new WorkOrderQuery(Overdue: false))).TotalCount);
        var firstPage = await fixture.WorkOrders.ListAsync(new WorkOrderQuery(PageSize: 2));
        var secondPage = await fixture.WorkOrders.ListAsync(new WorkOrderQuery(Page: 2, PageSize: 2));
        Assert.Equal(3, secondPage.TotalCount);
        Assert.Single(secondPage.Items);
        Assert.Empty(firstPage.Items.Select(order => order.Id).Intersect(secondPage.Items.Select(order => order.Id)));
        var calendar = await fixture.WorkOrders.CalendarAsync(new WorkOrderCalendarQuery(today.AddDays(-1), today.AddDays(-1),
            data.Machine.Id, data.Technician.Id, data.Supervisor.Id, WorkOrderLifecycleStatus.ASSIGNED));
        Assert.Equal(wanted.Id, Assert.Single(calendar).Id);
        Assert.DoesNotContain("Instructions", JsonSerializer.Serialize(calendar), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Items", JsonSerializer.Serialize(calendar), StringComparison.OrdinalIgnoreCase);
        var machine = await fixture.Db.Machines.SingleAsync(machine => machine.Id == data.Machine.Id);
        machine.Name = "New current machine name";
        await fixture.Db.SaveChangesAsync();
        Assert.Equal(wanted.Id, Assert.Single((await fixture.WorkOrders.ListAsync(new WorkOrderQuery(Search: data.Machine.Name))).Items).Id);
    }

    [Theory]
    [InlineData(WorkOrderLifecycleStatus.PLANNED, true)]
    [InlineData(WorkOrderLifecycleStatus.ASSIGNED, true)]
    [InlineData(WorkOrderLifecycleStatus.IN_PROGRESS, true)]
    [InlineData(WorkOrderLifecycleStatus.REJECTED, true)]
    [InlineData(WorkOrderLifecycleStatus.AWAITING_APPROVAL, false)]
    [InlineData(WorkOrderLifecycleStatus.APPROVED, false)]
    [InlineData(WorkOrderLifecycleStatus.CANCELLED, false)]
    public async Task Overdue_is_calculated_from_due_date_and_lifecycle(WorkOrderLifecycleStatus status, bool expected)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var today = DateOnly.FromDateTime(fixture.Clock.GetUtcNow().UtcDateTime);
        var data = await PlanningTestData.CreateAsync(fixture, today.AddDays(-1));
        data = data with { Request = data.Request with { FrequencyType = MaintenanceFrequencyType.WEEKLY } };
        await data.CreatePlanAsync(fixture);
        await fixture.Generation.GenerateDueAsync();
        var job = await fixture.Db.WorkOrders.SingleAsync();
        job.LifecycleStatus = status;
        await fixture.Db.SaveChangesAsync();

        Assert.Equal(expected, (await fixture.WorkOrders.GetAsync(job.Id)).WorkOrder.Overdue);
        Assert.Equal(expected ? 1 : 0, (await fixture.WorkOrders.ListAsync(new WorkOrderQuery(Overdue: true))).TotalCount);
    }

    [Fact]
    public async Task Overdue_changes_at_UTC_midnight_and_not_during_the_due_day()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        fixture.Clock.SetUtc(new DateTimeOffset(2026, 1, 1, 23, 59, 0, TimeSpan.Zero));
        var data = await PlanningTestData.CreateAsync(fixture);
        await data.CreatePlanAsync(fixture);
        await fixture.Generation.GenerateDueAsync();
        var jobId = await fixture.Db.WorkOrders.Select(order => order.Id).SingleAsync();

        Assert.False((await fixture.WorkOrders.GetAsync(jobId)).WorkOrder.Overdue);
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.True((await fixture.WorkOrders.GetAsync(jobId)).WorkOrder.Overdue);
    }

    [Fact]
    public async Task Invalid_date_ranges_enums_and_pagination_are_rejected()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var today = DateOnly.FromDateTime(fixture.Clock.GetUtcNow().UtcDateTime);
        await ModuleFixture.ExpectStatusAsync(400,
            () => fixture.WorkOrders.ListAsync(new WorkOrderQuery(PlannedFrom: today, PlannedTo: today.AddDays(-1))));
        await ModuleFixture.ExpectStatusAsync(400,
            () => fixture.WorkOrders.ListAsync(new WorkOrderQuery(DueFrom: today, DueTo: today.AddDays(-1))));
        await ModuleFixture.ExpectStatusAsync(400,
            () => fixture.WorkOrders.ListAsync(new WorkOrderQuery(LifecycleStatus: (WorkOrderLifecycleStatus)999)));
        await ModuleFixture.ExpectStatusAsync(400,
            () => fixture.WorkOrders.ListAsync(new WorkOrderQuery(PageSize: 101)));
        await ModuleFixture.ExpectStatusAsync(400,
            () => fixture.WorkOrders.CalendarAsync(new WorkOrderCalendarQuery(today, today.AddDays(-1))));
        await ModuleFixture.ExpectStatusAsync(400,
            () => fixture.WorkOrders.CalendarAsync(new WorkOrderCalendarQuery(today, today.AddDays(367))));
    }
}
