using System.Text.Json;
using MaintainPro.Application.Identity;
using MaintainPro.Application.Users;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class UserServiceTests
{
    [Fact]
    public async Task Admin_creates_user_with_multiple_roles_and_hashes_password_immediately()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var password = ModuleFixture.NewPassword();

        var dto = await fixture.Users.CreateAsync(new CreateUserRequest(" NEW-001 ", "New", "Person",
            " New.Person@Example.Invalid ", password, new[] { "TECHNICIAN", "SUPERVISOR" }));
        var entity = await fixture.Db.Users.Include(user => user.UserRoles).SingleAsync(user => user.Id == dto.Id);

        Assert.Equal(2, entity.UserRoles.Count);
        Assert.Equal(new[] { "SUPERVISOR", "TECHNICIAN" }, dto.Roles.Order());
        Assert.NotEqual(password, entity.PasswordHash);
        Assert.DoesNotContain(entity.PasswordHash, JsonSerializer.Serialize(dto));
        Assert.DoesNotContain("PasswordHash", JsonSerializer.Serialize(dto), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), audit => audit.EntityId == dto.Id.ToString()
            && audit.Action.Contains("Created", StringComparison.OrdinalIgnoreCase));
        await fixture.AssertAuditHasNoSecretsAsync(password, entity.PasswordHash);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Duplicate_employee_id_or_email_is_rejected_including_normalized_input(bool duplicateEmployee)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var existing = await fixture.SeedUserAsync("TECHNICIAN");
        var count = await fixture.Db.Users.CountAsync();

        var request = new CreateUserRequest(
            duplicateEmployee ? $" {existing.EmployeeId.ToLowerInvariant()} " : "UNIQUE-EMPLOYEE",
            "Another", "User", duplicateEmployee ? "unique@example.invalid" : $" {existing.Email.ToUpperInvariant()} ",
            fixture.Password, new[] { "TECHNICIAN" });

        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Users.CreateAsync(request));

        Assert.Equal(count, await fixture.Db.Users.CountAsync());
    }

    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("")]
    public async Task User_creation_rejects_invalid_roles(string role)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();

        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Users.CreateAsync(new CreateUserRequest(
            "NEW-001", "New", "Person", "new@example.invalid", fixture.Password, new[] { role })));
    }

    [Fact]
    public async Task User_creation_rejects_nonexistent_department()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();

        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Users.CreateAsync(new CreateUserRequest(
            "NEW-001", "New", "Person", "new@example.invalid", fixture.Password, new[] { "TECHNICIAN" },
            DepartmentId: Guid.NewGuid())));
    }

    [Fact]
    public async Task Status_deactivation_preserves_user_references_and_is_audited()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var user = await fixture.SeedUserAsync("TECHNICIAN");
        var machine = await fixture.SeedMachineAsync(user.Id);

        await fixture.Users.SetStatusAsync(user.Id, new UserStatusRequest(false));
        fixture.Db.ChangeTracker.Clear();

        var persisted = await fixture.Db.Users.SingleAsync(item => item.Id == user.Id);
        Assert.False(persisted.IsActive);
        Assert.Equal(user.Id, (await fixture.Db.Machines.FindAsync(machine.Id))!.MachineOwnerUserId);
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), audit => audit.EntityId == user.Id.ToString()
            && audit.Action.Contains("Status", StringComparison.OrdinalIgnoreCase));
        fixture.Db.Users.Remove(persisted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Role_replacement_preserves_multi_role_membership_and_is_audited()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var user = await fixture.SeedUserAsync("TECHNICIAN");
        var originalStamp = user.SecurityStamp;
        var login = await fixture.Auth.LoginAsync(new LoginRequest(user.Email, fixture.Password));

        var dto = await fixture.Users.SetRolesAsync(user.Id, new UserRolesRequest(new[] { "TECHNICIAN", "SUPERVISOR" }));

        Assert.Equal(new[] { "SUPERVISOR", "TECHNICIAN" }, dto.Roles.Order());
        Assert.Equal(2, await fixture.Db.UserRoles.CountAsync(role => role.UserId == user.Id));
        Assert.NotEqual(originalStamp, user.SecurityStamp);
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), audit => audit.EntityId == user.Id.ToString()
            && audit.Action.Contains("Role", StringComparison.OrdinalIgnoreCase));
        await ModuleFixture.ExpectStatusAsync(401,
            () => fixture.Auth.RefreshAsync(new RefreshRequest(login.RefreshToken)));
        await fixture.AssertAuditHasNoSecretsAsync(fixture.Password, user.PasswordHash);
    }

    [Theory]
    [InlineData("TECHNICIAN")]
    [InlineData("SUPERVISOR")]
    [InlineData("MANAGER")]
    public async Task Non_administrators_cannot_create_users(string role)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        fixture.ActAs(await fixture.SeedUserAsync(role));

        await ModuleFixture.ExpectStatusAsync(403, () => fixture.Users.CreateAsync(new CreateUserRequest(
            "NEW-001", "New", "Person", "new@example.invalid", fixture.Password, new[] { "TECHNICIAN" })));
    }
}
