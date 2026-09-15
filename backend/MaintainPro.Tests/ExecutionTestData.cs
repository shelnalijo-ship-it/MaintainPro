using MaintainPro.Application.Planning;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

internal sealed record ExecutionTestData(User Administrator, User Technician, User Supervisor,
    Machine Machine, MaintenancePlanDto Plan, WorkOrder WorkOrder)
{
    public static async Task<ExecutionTestData> CreateAsync(ModuleFixture fixture,
        ChecklistWriteRequest? checklist = null, bool photoRequired = false,
        int minimumPhotoCount = 0, bool commentRequired = false)
    {
        var planning = await PlanningTestData.CreateAsync(fixture);
        var plan = await fixture.Plans.CreateAsync(planning.Request with
        {
            PhotoRequired = photoRequired, MinimumPhotoCount = minimumPhotoCount,
            CommentRequired = commentRequired
        });
        await fixture.Plans.SetChecklistAsync(plan.Id, checklist ?? new ChecklistWriteRequest("Execution checklist",
            [new ChecklistItemWriteRequest(1, "Confirm inspection", ChecklistResponseType.CONFIRMATION)]));
        var generated = await fixture.Generation.GenerateDueAsync();
        Assert.Equal(1, generated.WorkOrdersCreated);
        var order = await fixture.Db.WorkOrders.Include(x => x.Definition).ThenInclude(x => x.Items)
            .SingleAsync(x => x.MaintenancePlanId == plan.Id);
        fixture.ActAs(planning.Technician);
        return new(planning.Administrator, planning.Technician, planning.Supervisor, planning.Machine, plan, order);
    }

    public WorkOrderChecklistItem Item => Assert.Single(WorkOrder.Definition.Items);

    public static byte[] Png() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a/e8AAAAASUVORK5CYII=");

    public static byte[] Pdf() => System.Text.Encoding.ASCII.GetBytes(
        "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n");
}
