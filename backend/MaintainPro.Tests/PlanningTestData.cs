using MaintainPro.Application.Planning;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Tests;

internal sealed record PlanningTestData(User Administrator, User Technician, User Supervisor,
    Machine Machine, MaintenanceTypeDto Type, PlanWriteRequest Request)
{
    public static async Task<PlanningTestData> CreateAsync(ModuleFixture fixture, DateOnly? start = null)
    {
        var administrator = await fixture.AsAdminAsync();
        var technician = await fixture.SeedUserAsync("TECHNICIAN");
        var supervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var machine = await fixture.SeedMachineAsync(technician.Id, supervisor.Id);
        var type = await fixture.MaintenanceTypes.CreateAsync(new MaintenanceTypeWriteRequest("Inspection", "Test definition"));
        var request = new PlanWriteRequest(machine.Id, "Pump inspection", type.Id, MaintenancePriority.HIGH,
            MaintenanceFrequencyType.DAILY, 1, start ?? DateOnly.FromDateTime(fixture.Clock.GetUtcNow().UtcDateTime),
            supervisor.Id, technician.Id, "Inspect bearings and pressure", 45, true, 2, true);
        return new(administrator, technician, supervisor, machine, type, request);
    }

    public static ChecklistWriteRequest Checklist(string name = "Inspection checklist") => new(name, new[]
    {
        new ChecklistItemWriteRequest(2, "Photograph condition", ChecklistResponseType.PHOTO, PhotoRequired: true),
        new ChecklistItemWriteRequest(1, "Measure pressure", ChecklistResponseType.NUMBER,
            Description: "Record stable gauge pressure", Unit: "bar", MinimumValue: 1.25m, MaximumValue: 8.75m,
            PhotoRequired: true)
    });

    public async Task<(MaintenancePlanDto Plan, ChecklistTemplateDto Checklist)> CreatePlanAsync(ModuleFixture fixture)
    {
        var plan = await fixture.Plans.CreateAsync(Request);
        var checklist = await fixture.Plans.SetChecklistAsync(plan.Id, Checklist());
        return (plan, checklist);
    }
}
