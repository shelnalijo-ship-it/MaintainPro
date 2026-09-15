using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Notifications;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class NotificationSettingsTests
{
    [Fact]
    public async Task Defaults_are_read_without_seeding_and_updates_persist_one_audited_policy()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var administrator = await fixture.AsAdminAsync();
        var defaults = await fixture.EscalationSettings.GetAsync();
        Assert.Equal((1, 1, 3, 5), (defaults.DueSoonDays, defaults.TechnicianOverdueDays,
            defaults.SupervisorEscalationDays, defaults.ManagerEscalationDays));
        Assert.Empty(await fixture.Db.EscalationSettings.ToListAsync());
        var changed = await fixture.EscalationSettings.UpdateAsync(new(2, 2, 4, 8));
        Assert.Equal(administrator.Id, changed.UpdatedByUserId);
        Assert.Equal(2, changed.DueSoonDays);
        Assert.Equal(8, changed.ManagerEscalationDays);
        var manager = await fixture.SeedUserAsync("MANAGER");
        fixture.ActAs(manager);
        await fixture.EscalationSettings.UpdateAsync(new(0, 0, 0, 0));
        Assert.Equal(1, await fixture.Db.EscalationSettings.CountAsync());
        Assert.Equal(0, (await fixture.EscalationSettings.GetAsync()).ManagerEscalationDays);
        Assert.Equal(2, await fixture.Db.AuditLogs.CountAsync(x => x.Action == "EscalationSettings.Updated"));
    }

    [Theory]
    [InlineData(-1, 1, 3, 5)]
    [InlineData(1, -1, 3, 5)]
    [InlineData(1, 1, -1, 5)]
    [InlineData(1, 1, 3, -1)]
    [InlineData(1, 4, 3, 5)]
    [InlineData(1, 1, 6, 5)]
    [InlineData(366, 1, 3, 5)]
    public async Task Invalid_policy_thresholds_are_rejected_without_saving(int soon, int technician, int supervisor, int manager)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.EscalationSettings.UpdateAsync(new(soon, technician, supervisor, manager)));
        Assert.Empty(await fixture.Db.EscalationSettings.ToListAsync());
    }

    [Theory]
    [InlineData("TECHNICIAN")]
    [InlineData("SUPERVISOR")]
    public async Task Settings_are_only_visible_and_editable_to_manager_or_admin(string role)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        fixture.ActAs(await fixture.SeedUserAsync(role));
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.EscalationSettings.GetAsync());
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.EscalationSettings.UpdateAsync(new(1, 1, 3, 5)));
    }

    [Fact]
    public async Task Failed_audit_rolls_back_policy_changes()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        await fixture.EscalationSettings.UpdateAsync(new(1, 1, 3, 5));
        var failing = new EscalationSettingsService(fixture.Db, fixture.Actor, new FailingAudit(), fixture.Clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.UpdateAsync(new(4, 2, 4, 6)));
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.EscalationSettings.GetAsync();
        Assert.Equal(1, saved.DueSoonDays);
        Assert.Equal(5, saved.ManagerEscalationDays);
    }

    private sealed class FailingAudit : IAuditWriter
    {
        public void Record(string action, string entityType, Guid? entityId, object? oldValues = null,
            object? newValues = null) => throw new InvalidOperationException("Simulated settings audit failure.");
    }
}
