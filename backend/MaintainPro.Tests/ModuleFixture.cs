using System.Security.Cryptography;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Audit;
using MaintainPro.Application.Common;
using MaintainPro.Application.Identity;
using MaintainPro.Application.Machines;
using MaintainPro.Application.MasterData;
using MaintainPro.Application.Users;
using MaintainPro.Application.Planning;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Application.Execution;
using MaintainPro.Application.Reviews;
using MaintainPro.Application.Notifications;
using MaintainPro.Application.Breakdowns;
using MaintainPro.Application.Calibrations;
using MaintainPro.Application.ExternalServices;
using MaintainPro.Application.Reporting;
using MaintainPro.Domain.Entities;
using MaintainPro.Infrastructure.Identity;
using MaintainPro.Infrastructure.Persistence;
using MaintainPro.Infrastructure.Planning;
using MaintainPro.Infrastructure.Files;
using MaintainPro.Infrastructure.Reporting;
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
    private readonly string fileStorageDirectory = Path.Combine(Path.GetTempPath(),
        $"maintainpro-execution-tests-{Guid.NewGuid():N}");

    private ModuleFixture(SqliteConnection connection, ApplicationDbContext db)
    {
        this.connection = connection;
        Db = db;
        Passwords = new PasswordService(new PasswordHasher<User>());
        Tokens = new TokenService(Options.Create(JwtSettings), Clock);
        Audit = new AuditWriter(db, Actor, Clock);
        AuditLogs = new AuditLogQueryService(db, Actor);
        Auth = new AuthService(db, Passwords, Tokens, Actor, Audit, Clock);
        Users = new UserService(db, Passwords, Actor, Audit, Clock);
        Machines = new MachineService(db, Actor, Audit, Clock);
        Masters = new MasterDataService(db, Actor, Audit);
        Recurrence = new RecurrenceService();
        MaintenanceTypes = new MaintenanceTypeService(db, Actor, Audit, Clock);
        Plans = new MaintenancePlanService(db, Actor, Audit, Clock, Recurrence);
        Numbers = new WorkOrderNumberAllocator(db);
        Generation = new WorkOrderGenerationService(db, Actor, Audit, Recurrence, Clock, Numbers,
            new SqliteGenerationConcurrency(db));
        WorkOrders = new WorkOrderService(db, Actor, Clock);
        Execution = new WorkOrderExecutionService(db, Actor, Audit, Clock);
        Submissions = new WorkOrderSubmissionService(db, Actor, Audit, Clock);
        Reviews = new WorkOrderReviewService(db, Actor, Audit, Clock);
        History = new WorkOrderHistoryService(db, Actor);
        FileStorage = new LocalFileStorageService(Options.Create(new FileStorageOptions { RootDirectory = fileStorageDirectory }));
        Evidence = new WorkOrderEvidenceService(db, Actor, Audit, Clock, FileStorage);
        NotificationWriter = new NotificationWriter(db, Clock);
        Notifications = new NotificationService(db, Actor, Clock);
        NotificationEvents = new NotificationEventService(db, Actor, Audit, Clock);
        EscalationSettings = new EscalationSettingsService(db, Actor, Audit, Clock);
        Escalations = new EscalationQueryService(db, Actor);
        Reminders = new ReminderProcessingService(db, Actor, Audit, Clock, NotificationEvents,
            new SqliteGenerationConcurrency(db), Generation);
        CorrectiveExecution = new CorrectiveExecutionService(db, Actor, Audit, Clock);
        CorrectiveSubmissions = new CorrectiveSubmissionService(db, Actor, Audit, Clock);
        CorrectiveReviews = new CorrectiveReviewService(db, Actor, Audit, Clock);
        BreakdownEvidence = new BreakdownEvidenceService(db, Actor, Audit, Clock, FileStorage);
        BreakdownNotifications = new BreakdownNotificationProcessor(db, Actor, Audit, Clock,
            new SqliteGenerationConcurrency(db));
        Breakdowns = new BreakdownService(db, Actor, Audit, Clock, new SqliteGenerationConcurrency(db));
        Calibrations = new CalibrationService(db, Actor, Audit, Clock, FileStorage);
        CalibrationRenewals = new CalibrationRenewalService(db, Actor, Audit, Clock);
        CalibrationReminders = new CalibrationReminderProcessor(db, Actor, Audit, Clock,
            new SqliteGenerationConcurrency(db));
        ExternalNumbers = new ExternalServiceNumberAllocator(db);
        ExternalServices = new ExternalServiceService(db, Actor, Audit, Clock, ExternalNumbers,
            new SqliteGenerationConcurrency(db));
        ExternalServiceAttachments = new ExternalServiceAttachmentService(db, Actor, Audit, Clock, FileStorage);
        MachineDocuments = new MachineDocumentService(db, Actor, Audit, Clock, FileStorage);
        ReportQueries = new ReportQueryService(db, Actor, Clock);
        MachineHistoryReports = new MachineHistoryReportService(db, Actor);
        SummaryReports = new SummaryReportingService(db, Actor, Clock, ReportQueries);
        Dashboards = new DashboardService(db, Actor, Clock, ReportQueries);
        ReportExporter = new ReportExportService(Options.Create(new ReportingOptions()));
        ReportExports = new ReportExportCoordinator(db, Actor, Audit, Clock, ReportExporter,
            ReportQueries, SummaryReports, MachineHistoryReports);
        ReportExportHistory = new ReportExportHistoryService(db, Actor);
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
    public AuditLogQueryService AuditLogs { get; }
    public AuthService Auth { get; }
    public UserService Users { get; }
    public MachineService Machines { get; }
    public MasterDataService Masters { get; }
    public RecurrenceService Recurrence { get; }
    public MaintenanceTypeService MaintenanceTypes { get; }
    public MaintenancePlanService Plans { get; }
    public WorkOrderNumberAllocator Numbers { get; }
    public WorkOrderGenerationService Generation { get; }
    public WorkOrderService WorkOrders { get; }
    public WorkOrderExecutionService Execution { get; }
    public WorkOrderSubmissionService Submissions { get; }
    public WorkOrderReviewService Reviews { get; }
    public WorkOrderHistoryService History { get; }
    public LocalFileStorageService FileStorage { get; }
    public WorkOrderEvidenceService Evidence { get; }
    public string FileStorageDirectory => fileStorageDirectory;
    public NotificationWriter NotificationWriter { get; }
    public NotificationService Notifications { get; }
    public NotificationEventService NotificationEvents { get; }
    public EscalationSettingsService EscalationSettings { get; }
    public EscalationQueryService Escalations { get; }
    public ReminderProcessingService Reminders { get; }
    public CorrectiveExecutionService CorrectiveExecution { get; }
    public CorrectiveSubmissionService CorrectiveSubmissions { get; }
    public CorrectiveReviewService CorrectiveReviews { get; }
    public BreakdownEvidenceService BreakdownEvidence { get; }
    public BreakdownNotificationProcessor BreakdownNotifications { get; }
    public BreakdownService Breakdowns { get; }
    public CalibrationService Calibrations { get; }
    public CalibrationRenewalService CalibrationRenewals { get; }
    public CalibrationReminderProcessor CalibrationReminders { get; }
    public ExternalServiceNumberAllocator ExternalNumbers { get; }
    public ExternalServiceService ExternalServices { get; }
    public ExternalServiceAttachmentService ExternalServiceAttachments { get; }
    public MachineDocumentService MachineDocuments { get; }
    public ReportQueryService ReportQueries { get; }
    public MachineHistoryReportService MachineHistoryReports { get; }
    public SummaryReportingService SummaryReports { get; }
    public DashboardService Dashboards { get; }
    public ReportExportService ReportExporter { get; }
    public ReportExportCoordinator ReportExports { get; }
    public ReportExportHistoryService ReportExportHistory { get; }

    public static async Task<ModuleFixture> CreateAsync(bool seedRoles = true, string? sqliteDatabasePath = null)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = sqliteDatabasePath ?? ":memory:", ForeignKeys = true,
            Pooling = false, DefaultTimeout = 5
        }.ToString());
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
        if (Directory.Exists(fileStorageDirectory))
        {
            var resolved = Path.GetFullPath(fileStorageDirectory);
            var testPrefix = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "maintainpro-execution-tests-");
            if (!resolved.StartsWith(testPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test evidence cleanup must remain inside its generated temporary directory.");
            Directory.Delete(resolved, recursive: true);
        }
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
    public void SetUtc(DateTimeOffset value) => now = value.ToUniversalTime();
}

internal sealed class SqliteGenerationConcurrency(ApplicationDbContext db) : IGenerationConcurrency
{
    private readonly GenerationConcurrency production = new(db);
    public void ResetTracking() => production.ResetTracking();
    public bool IsRetryable(Exception exception)
    {
        if (production.IsRetryable(exception)) return true;
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is SqliteException { SqliteErrorCode: 5 or 6 or 19 }) return true;
        return false;
    }
}
