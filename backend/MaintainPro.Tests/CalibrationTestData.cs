using MaintainPro.Application.Calibrations;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Tests;

internal sealed record CalibrationScenario(User Manager, User Administrator, User Supervisor,
    User Technician, Machine Machine, DateOnly Today);

internal static class CalibrationTestData
{
    public static async Task<CalibrationScenario> CreateAsync(ModuleFixture fixture,
        Action<Machine>? configure = null)
    {
        var now = DateTimeOffset.UtcNow;
        fixture.Clock.SetUtc(now);
        var manager = await fixture.SeedUserAsync("MANAGER");
        var administrator = await fixture.SeedUserAsync("ADMIN");
        var supervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var technician = await fixture.SeedUserAsync("TECHNICIAN");
        var machine = await fixture.SeedMachineAsync(technician.Id, supervisor.Id, item =>
        {
            item.CalibrationRequired = true;
            configure?.Invoke(item);
        });
        fixture.ActAs(manager);
        return new(manager, administrator, supervisor, technician, machine,
            DateOnly.FromDateTime(now.UtcDateTime));
    }

    public static Task<CalibrationCertificateDto> AddCertificateAsync(ModuleFixture fixture,
        CalibrationScenario data, string number = "CERT-001", int calibratedDaysAgo = 30,
        int expiresInDays = 90, CalibrationResult result = CalibrationResult.PASS) =>
        fixture.Calibrations.CreateAsync(new(data.Machine.Id, number, "Accredited Lab",
            data.Today.AddDays(-calibratedDaysAgo), data.Today.AddDays(expiresInDays), result));
}
