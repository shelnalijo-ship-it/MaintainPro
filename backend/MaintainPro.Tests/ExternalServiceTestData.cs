using MaintainPro.Application.ExternalServices;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Tests;

internal sealed record ExternalServiceScenario(User Manager, User Administrator, User Supervisor,
    User Technician, Machine Machine, DateOnly Today);

internal static class ExternalServiceTestData
{
    public static async Task<ExternalServiceScenario> CreateAsync(ModuleFixture fixture)
    {
        var now = DateTimeOffset.UtcNow;
        fixture.Clock.SetUtc(now);
        var manager = await fixture.SeedUserAsync("MANAGER");
        var administrator = await fixture.SeedUserAsync("ADMIN");
        var supervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var technician = await fixture.SeedUserAsync("TECHNICIAN");
        var machine = await fixture.SeedMachineAsync(technician.Id, supervisor.Id);
        fixture.ActAs(manager);
        return new(manager, administrator, supervisor, technician, machine,
            DateOnly.FromDateTime(now.UtcDateTime));
    }

    public static ExternalServiceWriteRequest Request(ExternalServiceScenario data,
        string company = "Precision Service LLC", string description = "Annual external service",
        DateOnly? followUp = null, DateOnly? nextService = null) => new(data.Machine.Id, company,
        "External Technician", data.Today.AddDays(-2), ExternalServiceType.PREVENTIVE_SERVICE,
        description, "Drive belt wear", "Inspected and adjusted", "Drive belt", 1250.50m,
        "PO-100", "INV-100", followUp, nextService, "Inspect belt tension", "Completed safely");
}
