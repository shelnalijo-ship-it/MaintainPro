using MaintainPro.Application.Execution;
using MaintainPro.Application.Planning;
using MaintainPro.Application.Reviews;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

internal sealed record NotificationTestData(User Administrator, User Technician, User Supervisor,
    Machine Machine, MaintenancePlanDto Plan, WorkOrder WorkOrder)
{
    public static async Task<NotificationTestData> CreateAsync(ModuleFixture fixture, int dueOffsetDays = 0,
        bool assignTechnician = true)
    {
        var administrator = await fixture.AsAdminAsync();
        var technician = await fixture.SeedUserAsync("TECHNICIAN");
        var supervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var machine = await fixture.SeedMachineAsync(technician.Id, supervisor.Id);
        var type = await fixture.MaintenanceTypes.CreateAsync(new MaintenanceTypeWriteRequest("Notification inspection"));
        var today = DateOnly.FromDateTime(fixture.Clock.GetUtcNow().UtcDateTime);
        // Yearly recurrence gives this fixture one occurrence whether its date is past or future.
        var plan = await fixture.Plans.CreateAsync(new(machine.Id, "Reminder test plan", type.Id,
            MaintenancePriority.HIGH, MaintenanceFrequencyType.YEARLY, 1, today.AddDays(dueOffsetDays),
            supervisor.Id, assignTechnician ? technician.Id : null));
        await fixture.Plans.SetChecklistAsync(plan.Id, new("Notification checklist",
            [new ChecklistItemWriteRequest(1, "Confirm inspection", ChecklistResponseType.CONFIRMATION)]));
        var summary = dueOffsetDays > 0
            ? await fixture.Generation.GenerateUpcomingAsync(dueOffsetDays)
            : await fixture.Generation.GenerateDueAsync();
        Assert.Equal(1, summary.WorkOrdersCreated);
        var order = await fixture.Db.WorkOrders.Include(x => x.Definition).ThenInclude(x => x.Items)
            .SingleAsync(x => x.MaintenancePlanId == plan.Id);
        return new(administrator, technician, supervisor, machine, plan, order);
    }

    public async Task<WorkOrderSubmissionDto> SubmitAsync(ModuleFixture fixture)
    {
        fixture.ActAs(Technician);
        await fixture.Execution.StartAsync(WorkOrder.Id);
        await fixture.Execution.SaveChecklistResultsAsync(WorkOrder.Id,
            new([new ChecklistResultRequest(Assert.Single(WorkOrder.Definition.Items).Id, ConfirmationValue: true)]));
        await fixture.Execution.CompleteAsync(WorkOrder.Id);
        return await fixture.Submissions.SubmitAsync(WorkOrder.Id);
    }
}
