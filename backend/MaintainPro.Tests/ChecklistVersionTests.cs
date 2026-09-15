using MaintainPro.Application.Planning;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class ChecklistVersionTests
{
    [Fact]
    public async Task Every_checklist_save_creates_an_ordered_immutable_version_even_when_unused()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var plan = await fixture.Plans.CreateAsync(data.Request);
        var request = PlanningTestData.Checklist();

        var first = await fixture.Plans.SetChecklistAsync(plan.Id, request);
        var second = await fixture.Plans.SetChecklistAsync(plan.Id, request with { Name = "Revised checklist" });
        var sameAgain = await fixture.Plans.SetChecklistAsync(plan.Id, request with { Name = "Revised checklist" });

        Assert.Equal(new[] { 1, 2 }, first.Items.Select(item => item.SequenceNumber));
        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
        Assert.Equal(3, sameAgain.Version);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(second.Id, sameAgain.Id);
        Assert.Equal(first.Name, (await fixture.Plans.GetChecklistAsync(plan.Id, 1)).Name);
        Assert.Equal(sameAgain.Id, (await fixture.Plans.GetChecklistAsync(plan.Id)).Id);
        Assert.Equal(3, await fixture.Db.ChecklistTemplates.CountAsync());
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), log => log.EntityType == nameof(ChecklistTemplate)
            && log.Action.Contains("Version", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("duplicate-sequence")]
    [InlineData("minimum-exceeds-maximum")]
    [InlineData("non-number-unit")]
    [InlineData("empty-title")]
    [InlineData("invalid-response")]
    [InlineData("zero-sequence")]
    public async Task Invalid_items_are_rejected_without_a_partial_template(string defect)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var plan = await fixture.Plans.CreateAsync(data.Request);
        var item = new ChecklistItemWriteRequest(1, "Pressure", ChecklistResponseType.NUMBER);
        IReadOnlyList<ChecklistItemWriteRequest> items = defect switch
        {
            "duplicate-sequence" => new[] { item, item with { Title = "Another" } },
            "minimum-exceeds-maximum" => new[] { item with { MinimumValue = 10, MaximumValue = 1 } },
            "non-number-unit" => new[] { item with { ResponseType = ChecklistResponseType.BOOLEAN, Unit = "bar" } },
            "empty-title" => new[] { item with { Title = " " } },
            "invalid-response" => new[] { item with { ResponseType = (ChecklistResponseType)999 } },
            _ => new[] { item with { SequenceNumber = 0 } }
        };

        await ModuleFixture.ExpectStatusAsync(400,
            () => fixture.Plans.SetChecklistAsync(plan.Id, new ChecklistWriteRequest("Invalid", items)));
        Assert.Empty(await fixture.Db.ChecklistTemplates.ToListAsync());
        Assert.Empty(await fixture.Db.ChecklistItems.ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task A_checklist_requires_between_one_and_two_hundred_items(int itemCount)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var plan = await fixture.Plans.CreateAsync(data.Request);
        var items = Enumerable.Range(1, itemCount)
            .Select(index => new ChecklistItemWriteRequest(index, $"Check {index}", ChecklistResponseType.CONFIRMATION)).ToArray();

        await ModuleFixture.ExpectStatusAsync(400,
            () => fixture.Plans.SetChecklistAsync(plan.Id, new ChecklistWriteRequest("Invalid size", items)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_templates_and_items_cannot_be_modified_or_deleted(bool generateWorkOrder)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var created = await data.CreatePlanAsync(fixture);
        if (generateWorkOrder) await fixture.Generation.GenerateDueAsync();
        fixture.Db.ChangeTracker.Clear();
        var template = await fixture.Db.ChecklistTemplates.SingleAsync();
        template.Name = "Attempted rewrite";

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        var item = await fixture.Db.ChecklistItems.OrderBy(item => item.SequenceNumber).FirstAsync();
        item.Title = "Attempted item rewrite";
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        fixture.Db.ChecklistTemplates.Remove(await fixture.Db.ChecklistTemplates.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(created.Checklist.Name, (await fixture.Plans.GetChecklistAsync(created.Plan.Id, 1)).Name);
    }

    [Fact]
    public async Task Used_version_edit_creates_a_new_version_and_preserves_generated_checklist()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var created = await data.CreatePlanAsync(fixture);
        await fixture.Generation.GenerateDueAsync();
        var workOrderId = await fixture.Db.WorkOrders.Select(order => order.Id).SingleAsync();

        var changed = await fixture.Plans.SetChecklistAsync(created.Plan.Id, new ChecklistWriteRequest("New checklist", new[]
        {
            new ChecklistItemWriteRequest(1, "Entirely new check", ChecklistResponseType.TEXT)
        }));
        var definition = (await fixture.WorkOrders.GetAsync(workOrderId)).Definition;

        Assert.Equal(2, changed.Version);
        Assert.Equal(created.Checklist.Id, definition.ChecklistTemplateId);
        Assert.Equal(1, definition.ChecklistVersion);
        Assert.Equal(created.Checklist.Name, definition.ChecklistName);
        Assert.Equal(created.Checklist.Items.Select(item => item.Title), definition.Items.Select(item => item.Title));
        Assert.Equal(created.Checklist.Id, (await fixture.Plans.GetChecklistAsync(created.Plan.Id, 1)).Id);
    }

    [Fact]
    public async Task Persisted_definitions_cannot_be_rewritten_or_extended_with_new_items()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await PlanningTestData.CreateAsync(fixture);
        var source = await data.CreatePlanAsync(fixture);
        await fixture.Generation.GenerateDueAsync();
        var definition = await fixture.Db.WorkOrderDefinitions.SingleAsync();
        definition.Instructions = "Attempted rewrite";
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        var item = await fixture.Db.WorkOrderChecklistItems.FirstAsync();
        item.Title = "Attempted rewrite";
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        fixture.Db.ChecklistItems.Add(new ChecklistItem
        {
            ChecklistTemplateId = source.Checklist.Id, SequenceNumber = 99,
            Title = "Additional live item", ResponseType = ChecklistResponseType.TEXT
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        fixture.Db.WorkOrderChecklistItems.Add(new WorkOrderChecklistItem
        {
            WorkOrderDefinitionId = definition.Id, SequenceNumber = 99,
            Title = "Additional historical item", ResponseType = ChecklistResponseType.TEXT
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        fixture.Db.WorkOrderDefinitions.Remove(await fixture.Db.WorkOrderDefinitions.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(2, await fixture.Db.WorkOrderChecklistItems.CountAsync());
        Assert.Equal(2, await fixture.Db.ChecklistItems.CountAsync());
    }
}
