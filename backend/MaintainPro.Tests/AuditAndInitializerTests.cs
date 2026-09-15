using MaintainPro.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace MaintainPro.Tests;

public sealed class AuditAndInitializerTests
{
    [Fact]
    public async Task Audit_records_cannot_be_changed_or_deleted_through_the_context()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var administrator = await fixture.AsAdminAsync();
        fixture.Audit.Record("test.created", "Example", administrator.Id, newValues: new { Name = "Original" });
        await fixture.Db.SaveChangesAsync();
        var log = await fixture.Db.AuditLogs.SingleAsync();
        log.Action = "tampered";

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        log = await fixture.Db.AuditLogs.SingleAsync();
        Assert.Equal("test.created", log.Action);
        fixture.Db.AuditLogs.Remove(log);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(1, await fixture.Db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Audit_writer_removes_sensitive_fields_recursively()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var sensitive = ModuleFixture.NewPassword();
        fixture.Audit.Record("test.redaction", "Example", null, newValues: new
        {
            Safe = "Visible", Password = sensitive, PasswordHash = sensitive,
            Nested = new { AccessToken = sensitive, RefreshToken = sensitive, SigningKey = sensitive },
            Items = new[] { new { ResetToken = sensitive, Safe = "Nested visible" } }
        });
        await fixture.Db.SaveChangesAsync();

        var values = (await fixture.Db.AuditLogs.SingleAsync()).NewValuesJson!;

        Assert.Contains("Visible", values);
        Assert.Contains("Nested visible", values);
        Assert.DoesNotContain(sensitive, values);
        await fixture.AssertAuditHasNoSecretsAsync(sensitive);
    }

    [Fact]
    public async Task Missing_bootstrap_configuration_still_initializes_roles_idempotently()
    {
        await using var fixture = await ModuleFixture.CreateAsync(seedRoles: false);
        var initializer = CreateInitializer(fixture, new Dictionary<string, string?>());

        await initializer.InitializeAsync();
        var originalIds = await fixture.Db.Roles.OrderBy(role => role.Name).Select(role => role.Id).ToListAsync();
        await initializer.InitializeAsync();

        Assert.Equal(new[] { "ADMIN", "MANAGER", "SUPERVISOR", "TECHNICIAN" },
            await fixture.Db.Roles.OrderBy(role => role.Name).Select(role => role.Name).ToListAsync());
        Assert.Equal(originalIds, await fixture.Db.Roles.OrderBy(role => role.Name).Select(role => role.Id).ToListAsync());
        Assert.Empty(await fixture.Db.Users.ToListAsync());
    }

    [Fact]
    public async Task Bootstrap_creates_only_the_first_administrator_and_never_overwrites_credentials()
    {
        await using var fixture = await ModuleFixture.CreateAsync(seedRoles: false);
        var password = ModuleFixture.NewPassword();
        var settings = new Dictionary<string, string?>
        {
            ["BootstrapAdmin:EmployeeId"] = "ADMIN-001", ["BootstrapAdmin:FirstName"] = "Initial",
            ["BootstrapAdmin:LastName"] = "Administrator", ["BootstrapAdmin:Email"] = "admin@example.invalid",
            ["BootstrapAdmin:Password"] = password
        };
        await CreateInitializer(fixture, settings).InitializeAsync();
        var first = await fixture.Db.Users.Include(user => user.UserRoles).ThenInclude(role => role.Role).SingleAsync();
        var originalHash = first.PasswordHash;
        settings["BootstrapAdmin:Password"] = ModuleFixture.NewPassword();
        settings["BootstrapAdmin:Email"] = "replacement@example.invalid";

        await CreateInitializer(fixture, settings).InitializeAsync();

        var persisted = Assert.Single(await fixture.Db.Users.ToListAsync());
        Assert.Equal(first.Id, persisted.Id);
        Assert.Equal("admin@example.invalid", persisted.Email);
        Assert.Equal(originalHash, persisted.PasswordHash);
        Assert.Equal("ADMIN", Assert.Single(persisted.UserRoles).Role.Name);
        Assert.True(fixture.Passwords.Verify(persisted, password).Succeeded);
        await fixture.AssertAuditHasNoSecretsAsync(password, originalHash, settings["BootstrapAdmin:Password"]!);
    }

    private static IdentityInitializer CreateInitializer(ModuleFixture fixture, Dictionary<string, string?> settings) =>
        new(fixture.Db, fixture.Passwords, fixture.Audit,
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            NullLogger<IdentityInitializer>.Instance, fixture.Clock);
}
