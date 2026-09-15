using MaintainPro.Application.Breakdowns;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Tests;

public sealed class BreakdownReadTests
{
    [Fact]
    public async Task Multiple_roles_union_assigned_reported_and_supervised_visibility_before_paging()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var administrator = await fixture.AsAdminAsync();
        var member = await fixture.SeedUserAsync("TECHNICIAN", "SUPERVISOR");
        var technician = await fixture.SeedUserAsync("TECHNICIAN");
        var supervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var ownedMachine = await fixture.SeedMachineAsync(member.Id, supervisor.Id);
        var supervisedMachine = await fixture.SeedMachineAsync(technician.Id, member.Id);
        var otherMachine = await fixture.SeedMachineAsync(technician.Id, supervisor.Id);
        var assigned = await fixture.Breakdowns.ReportAsync(new(otherMachine.Id, BreakdownSeverity.HIGH, false, "Assigned inspection"));
        await fixture.Breakdowns.AssignAsync(assigned.Id, new(member.Id));
        fixture.ActAs(member);
        var reported = await fixture.Breakdowns.ReportAsync(new(ownedMachine.Id, BreakdownSeverity.MEDIUM, false, "Hydraulic seal leak"));
        fixture.ActAs(administrator);
        await fixture.Breakdowns.AssignAsync(reported.Id, new(technician.Id));
        var supervised = await fixture.Breakdowns.ReportAsync(new(supervisedMachine.Id, BreakdownSeverity.LOW, false, "Supervised inspection"));
        var hidden = await fixture.Breakdowns.ReportAsync(new(otherMachine.Id, BreakdownSeverity.LOW, false, "Unrelated inspection"));
        fixture.ActAs(member);

        var all = await fixture.Breakdowns.ListAsync(new());
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(new[] { assigned.Id, reported.Id, supervised.Id }.Order(), all.Items.Select(x => x.Id).Order());
        var pages = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await fixture.Breakdowns.ListAsync(new(Page: page, PageSize: 1));
            Assert.Equal(3, result.TotalCount);
            pages.Add(Assert.Single(result.Items).Id);
        }
        Assert.Equal(3, pages.Distinct().Count());
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Breakdowns.GetAsync(hidden.Id));
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Breakdowns.HistoryAsync(hidden.Id));
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.Breakdowns.AssignmentsAsync(hidden.Id));

        fixture.Actor.Roles = ["TECHNICIAN"];
        Assert.Equal(2, (await fixture.Breakdowns.ListAsync(new())).TotalCount);
        fixture.Actor.Roles = ["SUPERVISOR"];
        Assert.Equal(supervised.Id, Assert.Single((await fixture.Breakdowns.ListAsync(new())).Items).Id);
        fixture.ActAs(administrator);
        Assert.Equal(4, (await fixture.Breakdowns.ListAsync(new())).TotalCount);
        fixture.ActAs(await fixture.SeedUserAsync("MANAGER"));
        Assert.Equal(4, (await fixture.Breakdowns.ListAsync(new())).TotalCount);
    }

    [Fact]
    public async Task Search_and_combined_filters_use_reported_facts_and_machine_history_forces_its_machine()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await BreakdownTestData.CreateAsync(fixture, stopped: false);
        var day = DateOnly.FromDateTime(data.Breakdown.ReportedAt);
        var filters = new BreakdownQuery(MachineId: data.Machine.Id, TechnicianId: data.Technician.Id,
            SupervisorId: data.Supervisor.Id, Severity: BreakdownSeverity.HIGH, Status: BreakdownStatus.ASSIGNED,
            MachineStopped: false, ReportedFrom: day, ReportedTo: day, OpenOnly: true);
        Assert.Equal(data.Breakdown.Id, Assert.Single((await fixture.Breakdowns.ListAsync(filters)).Items).Id);
        foreach (var term in new[] { data.Breakdown.BreakdownNumber, data.Machine.MachineCode, data.Machine.Name, "bearing seized" })
            Assert.Equal(data.Breakdown.Id, Assert.Single((await fixture.Breakdowns.ListAsync(new(Search: term))).Items).Id);
        Assert.Empty((await fixture.Breakdowns.ListAsync(filters with { ReportedFrom = day.AddDays(1), ReportedTo = null })).Items);
        Assert.Empty((await fixture.Breakdowns.ListAsync(filters with { Severity = BreakdownSeverity.CRITICAL })).Items);
        Assert.Single((await fixture.Breakdowns.ListAsync(new(OpenOnly: false))).Items);
        var history = await fixture.Breakdowns.MachineHistoryAsync(data.Machine.Id, new(MachineId: Guid.NewGuid()));
        Assert.Equal(data.Breakdown.Id, Assert.Single(history.Items).Id);
        fixture.ActAs(await fixture.SeedUserAsync("TECHNICIAN"));
        Assert.Empty((await fixture.Breakdowns.ListAsync(new())).Items);
    }

    [Theory]
    [InlineData("page")]
    [InlineData("size")]
    [InlineData("severity")]
    [InlineData("status")]
    [InlineData("range")]
    public async Task Invalid_list_queries_are_rejected(string invalid)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var query = invalid switch
        {
            "page" => new BreakdownQuery(Page: 0),
            "size" => new BreakdownQuery(PageSize: 101),
            "severity" => new BreakdownQuery(Severity: (BreakdownSeverity)900),
            "status" => new BreakdownQuery(Status: (BreakdownStatus)900),
            _ => new BreakdownQuery(ReportedFrom: new(2026, 9, 16), ReportedTo: new(2026, 9, 15))
        };
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Breakdowns.ListAsync(query));
    }
}
