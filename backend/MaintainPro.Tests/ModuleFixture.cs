using System.Security.Cryptography;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Audit;
using MaintainPro.Application.Common;
using MaintainPro.Application.Identity;
using MaintainPro.Application.Machines;
using MaintainPro.Application.MasterData;
using MaintainPro.Application.Users;
using MaintainPro.Domain.Entities;
using MaintainPro.Infrastructure.Identity;
using MaintainPro.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MaintainPro.Tests;

/// <summary>
/// Each test owns a separate, ephemeral SQLite connection. No user secrets, network connection,
/// application configuration, or MaintainPro development database are loaded by this fixture.
/// </summary>
internal sealed class ModuleFixture : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private int nextUser;
    private int nextMachine;

    private ModuleFixture(SqliteConnection connection, ApplicationDbContext db)
    {
        this.connection = connection;
        Db = db;
        Passwords = new PasswordService(new PasswordHasher<User>());
        Tokens = new TokenService(Options.Create(JwtSettings), Clock);
        Audit = new AuditWriter(db, Actor, Clock);
        Auth = new AuthService(db, Passwords, Tokens, Actor, Audit, Clock);
        Users = new UserService(db, Passwords, Actor, Audit, Clock);
        Machines = new MachineService(db, Actor, Audit);
        Masters = new MasterDataService(db, Actor, Audit);
    }

    public ApplicationDbContext Db { get; }
    public TestCurrentUser Actor { get; } = new();
    public AdjustableTimeProvider Clock { get; } = new();
    public string Password { get; } = NewPassword();
    public JwtOptions JwtSettings { get; } = new()
    {
        Issuer = "MaintainPro.Tests",
        Audience = "MaintainPro.Tests.Client",
        SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
    };
    public PasswordService Passwords { get; }
    public TokenService Tokens { get; }
    public AuditWriter Audit { get; }
    public AuthService Auth { get; }
    public UserService Users { get; }
    public MachineService Machines { get; }
    public MasterDataService Masters { get; }

    public static async Task<ModuleFixture> CreateAsync(bool seedRoles = true)
    {
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var fixture = new ModuleFixture(connection, db);
        if (seedRoles)
        {
            db.Roles.AddRange(new[] { "TECHNICIAN", "SUPERVISOR", "MANAGER", "ADMIN" }
                .Select(name => new Role { Name = name }));
            await db.SaveChangesAsync();
        }
        return fixture;
    }

    public async Task<User> SeedUserAsync(params string[] roles)
    {
        var number = ++nextUser;
        var user = new User
        {
            EmployeeId = $"EMP-{number:D4}", FirstName = "Test", LastName = $"Person{number}",
            Email = $"person{number}@example.invalid", PasswordHash = string.Empty
        };
        user.PasswordHash = Passwords.Hash(user, Password);
        foreach (var roleName in roles.Distinct())
        {
            var role = await Db.Roles.SingleAsync(item => item.Name == roleName);
            user.UserRoles.Add(new UserRole { User = user, UserId = user.Id, Role = role, RoleId = role.Id });
        }
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        return user;
    }

    public async Task<User> AsAdminAsync()
    {
        var user = await SeedUserAsync("ADMIN");
        ActAs(user);
        return user;
    }

    public void ActAs(User user)
    {
        Actor.UserId = user.Id;
        Actor.Roles = user.UserRoles.Select(membership => membership.Role.Name).ToArray();
    }

    public async Task<Machine> SeedMachineAsync(Guid? owner = null, Guid? supervisor = null,
        Action<Machine>? configure = null)
    {
        var number = ++nextMachine;
        var machine = new Machine
        {
            MachineCode = $"MC-{number:D4}", Name = $"Test machine {number}",
            MachineOwnerUserId = owner, SupervisorUserId = supervisor
        };
        configure?.Invoke(machine);
        Db.Machines.Add(machine);
        await Db.SaveChangesAsync();
        return machine;
    }

    public async Task AssertAuditHasNoSecretsAsync(params string[] secrets)
    {
        var values = await Db.AuditLogs.Select(log => new { log.OldValuesJson, log.NewValuesJson }).ToListAsync();
        var json = string.Join("\n", values.Select(value => value.OldValuesJson + value.NewValuesJson));
        foreach (var secret in secrets.Where(value => !string.IsNullOrEmpty(value)))
            Assert.DoesNotContain(secret, json);
        Assert.DoesNotContain("PasswordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RefreshToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AccessToken", json, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<AppException> ExpectStatusAsync(int status, Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<AppException>(action);
        Assert.Equal(status, error.StatusCode);
        return error;
    }

    public static string NewPassword() => "Test!a7-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await connection.DisposeAsync();
    }
}

internal sealed class TestCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
    public string? IpAddress => "127.0.0.1";
    public string? DeviceInfo => "MaintainPro isolated automated tests";
}

internal sealed class AdjustableTimeProvider : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan duration) => now = now.Add(duration);
}
