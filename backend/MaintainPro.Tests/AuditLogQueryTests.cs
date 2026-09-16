using System.Text.Json;
using MaintainPro.Application.Audit;
using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class AuditLogQueryTests
{
    [Fact]
    public async Task Manager_can_filter_before_paging_and_receives_newest_records_first_without_tracking()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var manager = await fixture.SeedUserAsync("MANAGER");
        var otherUser = await fixture.SeedUserAsync("TECHNICIAN");
        fixture.Db.AuditLogs.AddRange(
            Log(manager.Id, "Machine.Updated", "Machine", "machine-1", new DateTime(2026, 9, 12, 8, 0, 0, DateTimeKind.Utc)),
            Log(manager.Id, "Machine.Updated", "Machine", "machine-2", new DateTime(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc)),
            Log(otherUser.Id, "Machine.Created", "Machine", "machine-3", new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc)),
            Log(manager.Id, "Machine.Updated", "Machine", "machine-4", new DateTime(2026, 9, 13, 8, 0, 0, DateTimeKind.Utc)));
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        fixture.ActAs(manager);

        var result = await fixture.AuditLogs.ListAsync(new AuditLogQuery(
            From: new DateOnly(2026, 9, 12), To: new DateOnly(2026, 9, 12), UserId: manager.Id,
            Action: "Machine.Updated", EntityType: "Machine", Page: 1, PageSize: 1));

        Assert.Equal(2, result.TotalCount);
        var row = Assert.Single(result.Items);
        Assert.Equal("machine-2", row.EntityId);
        Assert.Equal(manager.Id, row.User?.Id);
        Assert.Equal(manager.EmployeeId, row.User?.EmployeeId);
        Assert.Empty(fixture.Db.ChangeTracker.Entries<AuditLog>());
    }

    [Fact]
    public async Task Query_removes_sensitive_legacy_json_and_does_not_echo_malformed_values()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var administrator = await fixture.AsAdminAsync();
        var secret = ModuleFixture.NewPassword();
        fixture.Db.AuditLogs.AddRange(
            new AuditLog
            {
                UserId = administrator.Id, Action = "Legacy.Updated", EntityType = "Legacy", EntityId = "1",
                OldValuesJson = JsonSerializer.Serialize(new
                {
                    Safe = "visible", PasswordHash = secret, Nested = new { RefreshToken = secret }
                }),
                NewValuesJson = "not-json", IpAddress = "127.0.0.1", DeviceInfo = "Bearer header-value",
                CreatedAt = new DateTime(2026, 9, 12, 8, 0, 0, DateTimeKind.Utc)
            });
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var row = Assert.Single((await fixture.AuditLogs.ListAsync(new AuditLogQuery())).Items);

        Assert.Contains("visible", row.OldValuesJson);
        Assert.DoesNotContain(secret, row.OldValuesJson);
        Assert.DoesNotContain("PasswordHash", row.OldValuesJson);
        Assert.Null(row.NewValuesJson);
        Assert.Equal("127.0.0.1", row.IpAddress);
        Assert.Null(row.DeviceInfo);
    }

    [Theory]
    [InlineData("SUPERVISOR")]
    [InlineData("TECHNICIAN")]
    public async Task Query_rejects_roles_outside_management(string role)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        fixture.ActAs(await fixture.SeedUserAsync(role));

        await ModuleFixture.ExpectStatusAsync(403, () => fixture.AuditLogs.ListAsync(new AuditLogQuery()));
    }

    [Fact]
    public async Task Query_rejects_invalid_date_ranges()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        fixture.ActAs(await fixture.SeedUserAsync("MANAGER"));

        await ModuleFixture.ExpectStatusAsync(400, () => fixture.AuditLogs.ListAsync(new AuditLogQuery(
            From: new DateOnly(2026, 9, 13), To: new DateOnly(2026, 9, 12))));
    }

    private static AuditLog Log(Guid userId, string action, string entityType, string entityId, DateTime createdAt) => new()
    {
        UserId = userId, Action = action, EntityType = entityType, EntityId = entityId,
        OldValuesJson = "{}", NewValuesJson = "{}", CreatedAt = createdAt
    };
}
