using System.Data;
using MaintainPro.Application.Abstractions;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Infrastructure;
using MaintainPro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MaintainPro.Tests;

public class ApplicationDbContextTests
{
    // Deliberately unusable test configuration. These tests never open a connection.
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=1;Database=maintainpro_model_tests;Username=model_test";

    [Fact]
    public void Model_contains_the_module_entities_without_shadow_properties()
    {
        using var context = CreateContext();
        var entities = context.Model.GetEntityTypes().ToArray();

        Assert.Equal(new[]
        {
            nameof(AuditLog), nameof(Department), nameof(Location), nameof(Machine), nameof(MachineAssignmentHistory),
            nameof(MachineCategory), nameof(RefreshToken), nameof(Role), nameof(User), nameof(UserRole),
            nameof(MaintenanceType), nameof(MaintenancePlan), nameof(ChecklistTemplate), nameof(ChecklistItem),
            nameof(WorkOrder), nameof(WorkOrderDefinition), nameof(WorkOrderChecklistItem), nameof(WorkOrderNumberSequence),
            nameof(WorkOrderExecution),
            nameof(WorkOrderChecklistResult),
            nameof(SparePartUsage),
            nameof(WorkOrderDefect),
            nameof(FileRecord),
            nameof(WorkOrderAttachment),
            nameof(WorkOrderSubmission),
            nameof(WorkOrderSubmissionChecklistResult),
            nameof(WorkOrderSubmissionAttachment),
            nameof(WorkOrderSubmissionPartUsage),
            nameof(WorkOrderSubmissionDefect),
            nameof(WorkOrderApproval),
            nameof(WorkOrderHistoryEvent),
            nameof(Notification),
            nameof(NotificationDeliveryAttempt),
            nameof(WorkOrderEscalation),
            nameof(EscalationSettings),
            nameof(NotificationEvent)
        }.Order(StringComparer.Ordinal),
            entities.Select(entity => entity.ClrType.Name).Order(StringComparer.Ordinal));

        Assert.All(entities.SelectMany(entity => entity.GetProperties()),
            property => Assert.False(property.IsShadowProperty(), property.Name));
        Assert.All(entities, entity => Assert.Empty(entity.GetSkipNavigations()));
        Assert.Null(context.Model.FindEntityType(typeof(User))!.FindProperty("RoleId"));
    }

    [Fact]
    public void Relationships_have_the_expected_foreign_keys_and_nullability()
    {
        using var context = CreateContext();
        var relationships = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys())
            .Select(foreignKey =>
                $"{foreignKey.DeclaringEntityType.ClrType.Name}.{foreignKey.DependentToPrincipal?.Name}: " +
                $"{string.Join(",", foreignKey.Properties.Select(property => property.Name))} -> " +
                $"{foreignKey.PrincipalEntityType.ClrType.Name}; required={foreignKey.IsRequired}")
            .Order(StringComparer.Ordinal);

        Assert.Equal(new[]
        {
            "User.Department: DepartmentId -> Department; required=False",
            "UserRole.User: UserId -> User; required=True",
            "UserRole.Role: RoleId -> Role; required=True",
            "Location.Department: DepartmentId -> Department; required=False",
            "Machine.MachineOwner: MachineOwnerUserId -> User; required=False",
            "Machine.Supervisor: SupervisorUserId -> User; required=False",
            "Machine.Category: CategoryId -> MachineCategory; required=False",
            "Machine.Department: DepartmentId -> Department; required=False",
            "Machine.Location: LocationId -> Location; required=False",
            "MachineAssignmentHistory.Machine: MachineId -> Machine; required=True",
            "MachineAssignmentHistory.Technician: TechnicianId -> User; required=True",
            "MachineAssignmentHistory.Supervisor: SupervisorId -> User; required=False",
            "MachineAssignmentHistory.AssignedByUser: AssignedByUserId -> User; required=True",
            "AuditLog.User: UserId -> User; required=False",
            "RefreshToken.User: UserId -> User; required=True",
            "RefreshToken.ReplacedByToken: ReplacedByTokenId -> RefreshToken; required=False",
            "MaintenancePlan.Machine: MachineId -> Machine; required=True",
            "MaintenancePlan.MaintenanceType: MaintenanceTypeId -> MaintenanceType; required=True",
            "MaintenancePlan.DefaultTechnician: DefaultTechnicianId -> User; required=False",
            "MaintenancePlan.Supervisor: SupervisorId -> User; required=True",
            "MaintenancePlan.CreatedByUser: CreatedByUserId -> User; required=True",
            "ChecklistTemplate.MaintenancePlan: MaintenancePlanId -> MaintenancePlan; required=True",
            "ChecklistTemplate.CreatedByUser: CreatedByUserId -> User; required=True",
            "ChecklistItem.ChecklistTemplate: ChecklistTemplateId -> ChecklistTemplate; required=True",
            "WorkOrder.Machine: MachineId -> Machine; required=True",
            "WorkOrder.MaintenancePlan: MaintenancePlanId -> MaintenancePlan; required=False",
            "WorkOrder.AssignedTechnician: AssignedTechnicianId -> User; required=False",
            "WorkOrder.Supervisor: SupervisorId -> User; required=True",
            "WorkOrderDefinition.WorkOrder: WorkOrderId -> WorkOrder; required=True",
            "WorkOrderDefinition.MaintenanceType: MaintenanceTypeId -> MaintenanceType; required=True",
            "WorkOrderDefinition.ChecklistTemplate: ChecklistTemplateId -> ChecklistTemplate; required=True",
            "WorkOrderChecklistItem.Definition: WorkOrderDefinitionId -> WorkOrderDefinition; required=True",
            "WorkOrderExecution.WorkOrder: WorkOrderId -> WorkOrder; required=True",
            "WorkOrderExecution.Technician: TechnicianId -> User; required=True",
            "WorkOrderChecklistResult.WorkOrder: WorkOrderId -> WorkOrder; required=True",
            "WorkOrderChecklistResult.WorkOrderChecklistItem: WorkOrderChecklistItemId -> WorkOrderChecklistItem; required=True",
            "WorkOrderChecklistResult.CompletedByUser: CompletedByUserId -> User; required=True",
            "SparePartUsage.WorkOrder: WorkOrderId -> WorkOrder; required=True",
            "SparePartUsage.CreatedByUser: CreatedByUserId -> User; required=True",
            "WorkOrderDefect.WorkOrder: WorkOrderId -> WorkOrder; required=True",
            "WorkOrderDefect.CreatedByUser: CreatedByUserId -> User; required=True",
            "FileRecord.UploadedByUser: UploadedByUserId -> User; required=True",
            "WorkOrderAttachment.WorkOrder: WorkOrderId -> WorkOrder; required=True",
            "WorkOrderAttachment.File: FileId -> FileRecord; required=True",
            "WorkOrderAttachment.WorkOrderChecklistItem: WorkOrderChecklistItemId -> WorkOrderChecklistItem; required=False",
            "WorkOrderAttachment.UploadedByUser: UploadedByUserId -> User; required=True",
            "WorkOrderSubmission.WorkOrder: WorkOrderId -> WorkOrder; required=True",
            "WorkOrderSubmission.SubmittedByUser: SubmittedByUserId -> User; required=True",
            "WorkOrderSubmissionChecklistResult.Submission: WorkOrderSubmissionId -> WorkOrderSubmission; required=True",
            "WorkOrderSubmissionChecklistResult.WorkOrderChecklistItem: WorkOrderChecklistItemId -> WorkOrderChecklistItem; required=True",
            "WorkOrderSubmissionChecklistResult.CompletedByUser: CompletedByUserId -> User; required=True",
            "WorkOrderSubmissionAttachment.Submission: WorkOrderSubmissionId -> WorkOrderSubmission; required=True",
            "WorkOrderSubmissionAttachment.File: FileId -> FileRecord; required=True",
            "WorkOrderSubmissionAttachment.WorkOrderChecklistItem: WorkOrderChecklistItemId -> WorkOrderChecklistItem; required=False",
            "WorkOrderSubmissionAttachment.UploadedByUser: UploadedByUserId -> User; required=True",
            "WorkOrderSubmissionPartUsage.Submission: WorkOrderSubmissionId -> WorkOrderSubmission; required=True",
            "WorkOrderSubmissionPartUsage.CreatedByUser: CreatedByUserId -> User; required=True",
            "WorkOrderSubmissionDefect.Submission: WorkOrderSubmissionId -> WorkOrderSubmission; required=True",
            "WorkOrderSubmissionDefect.CreatedByUser: CreatedByUserId -> User; required=True",
            "WorkOrderApproval.WorkOrder: WorkOrderId -> WorkOrder; required=True",
            "WorkOrderApproval.Submission: WorkOrderSubmissionId -> WorkOrderSubmission; required=True",
            "WorkOrderApproval.Supervisor: SupervisorId -> User; required=True",
            "WorkOrderHistoryEvent.WorkOrder: WorkOrderId -> WorkOrder; required=True",
            "WorkOrderHistoryEvent.ActorUser: ActorUserId -> User; required=False",
            "WorkOrderHistoryEvent.Submission: WorkOrderSubmissionId -> WorkOrderSubmission; required=False",
            "Notification.User: UserId -> User; required=True",
            "NotificationDeliveryAttempt.Notification: NotificationId -> Notification; required=True",
            "WorkOrderEscalation.WorkOrder: WorkOrderId -> WorkOrder; required=True",
            "WorkOrderEscalation.RecipientUser: RecipientUserId -> User; required=True",
            "WorkOrderEscalation.Notification: NotificationId -> Notification; required=True",
            "EscalationSettings.UpdatedByUser: UpdatedByUserId -> User; required=False",
            "NotificationEvent.WorkOrder: WorkOrderId -> WorkOrder; required=True"
        }.Order(StringComparer.Ordinal), relationships);
    }

    [Fact]
    public void Every_relationship_restricts_deletion_to_preserve_references_and_history()
    {
        using var context = CreateContext();
        var foreignKeys = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys()).ToArray();

        Assert.NotEmpty(foreignKeys);
        Assert.All(foreignKeys,
            foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void Required_business_identifiers_have_unique_indexes()
    {
        using var context = CreateContext();
        var uniqueIndexes = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetIndexes())
            .Where(index => index.IsUnique)
            .Select(index => $"{index.DeclaringEntityType.ClrType.Name}." +
                string.Join(",", index.Properties.Select(property => property.Name)))
            .Order(StringComparer.Ordinal);

        Assert.Equal(new[]
        {
            "Role.Name", "User.EmployeeId", "User.Email", "Department.Name",
            "MachineCategory.Name", "Machine.MachineCode", "RefreshToken.TokenHash",
            "MachineAssignmentHistory.MachineId", "MaintenanceType.Name",
            "ChecklistTemplate.MaintenancePlanId,Version", "ChecklistItem.ChecklistTemplateId,SequenceNumber",
            "WorkOrder.WorkOrderNumber", "WorkOrder.MaintenancePlanId,PlannedDate",
            "WorkOrderDefinition.WorkOrderId", "WorkOrderChecklistItem.WorkOrderDefinitionId,SequenceNumber",
            "WorkOrderExecution.WorkOrderId", "WorkOrderChecklistResult.WorkOrderId,WorkOrderChecklistItemId",
            "FileRecord.StorageKey", "WorkOrderSubmission.WorkOrderId,VersionNumber",
            "WorkOrderSubmissionChecklistResult.WorkOrderSubmissionId,WorkOrderChecklistItemId",
            "WorkOrderSubmissionAttachment.WorkOrderSubmissionId,FileId",
            "WorkOrderApproval.WorkOrderSubmissionId", "WorkOrderHistoryEvent.WorkOrderId,SequenceNumber",
            "Notification.DeduplicationKey", "NotificationDeliveryAttempt.NotificationId,Channel,AttemptNumber",
            "WorkOrderEscalation.DeduplicationKey", "WorkOrderEscalation.NotificationId",
            "NotificationEvent.DeduplicationKey"
        }.Order(StringComparer.Ordinal), uniqueIndexes);
    }

    [Fact]
    public void A_machine_has_at_most_one_open_assignment_and_session_hashes_are_unique()
    {
        using var context = CreateContext();
        var assignment = context.Model.FindEntityType(typeof(MachineAssignmentHistory))!;
        var activeIndex = Assert.Single(assignment.GetIndexes(), index => index.IsUnique);

        Assert.Equal(new[] { nameof(MachineAssignmentHistory.MachineId) },
            activeIndex.Properties.Select(property => property.Name));
        Assert.Equal("\"EffectiveTo\" IS NULL", activeIndex.GetFilter());

        var token = context.Model.FindEntityType(typeof(RefreshToken))!;
        Assert.Contains(token.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "TokenHash" }));
        Assert.DoesNotContain(token.GetProperties(), property =>
            property.Name is "Token" or "RefreshToken" or "PlaintextToken");
    }

    [Fact]
    public void Mutable_users_machines_and_sessions_have_optimistic_concurrency_protection()
    {
        using var context = CreateContext();

        foreach (var type in new[] { typeof(User), typeof(Machine), typeof(RefreshToken),
            typeof(MaintenanceType), typeof(MaintenancePlan), typeof(WorkOrder), typeof(WorkOrderNumberSequence) })
        {
            var entity = context.Model.FindEntityType(type)!;
            Assert.Contains(entity.GetProperties(), property => property.IsConcurrencyToken);
        }
    }

    [Fact]
    public void Persistent_occurrence_and_year_counter_keys_prevent_duplicate_generation()
    {
        using var context = CreateContext();
        var workOrder = context.Model.FindEntityType(typeof(WorkOrder))!;
        var occurrence = Assert.Single(workOrder.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "MaintenancePlanId", "PlannedDate" }));
        Assert.Equal("\"MaintenancePlanId\" IS NOT NULL", occurrence.GetFilter());
        var counter = context.Model.FindEntityType(typeof(WorkOrderNumberSequence))!;
        Assert.Equal(new[] { "Year" }, counter.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(typeof(long), counter.FindProperty("LastValue")!.ClrType);
        Assert.DoesNotContain("OVERDUE", Enum.GetNames<WorkOrderLifecycleStatus>());
        Assert.DoesNotContain("ESCALATED", Enum.GetNames<WorkOrderLifecycleStatus>());
    }

    [Fact]
    public void UserRole_has_a_composite_key_and_supports_two_roles_for_one_user()
    {
        using var context = CreateContext();
        var joinEntity = context.Model.FindEntityType(typeof(UserRole))!;

        Assert.Equal(new[] { nameof(UserRole.UserId), nameof(UserRole.RoleId) },
            joinEntity.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(nameof(User.UserRoles), joinEntity.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(User))
            .PrincipalToDependent!.Name);
        Assert.Equal(nameof(Role.UserRoles), joinEntity.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Role))
            .PrincipalToDependent!.Name);

        var user = new User
        {
            Id = Guid.NewGuid(), EmployeeId = "TEST-001", FirstName = "Test",
            LastName = "User", Email = "test@example.invalid", PasswordHash = "test-only-hash"
        };
        var technician = new Role { Id = Guid.NewGuid(), Name = "TECHNICIAN" };
        var supervisor = new Role { Id = Guid.NewGuid(), Name = "SUPERVISOR" };
        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id, User = user, RoleId = technician.Id, Role = technician
        });
        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id, User = user, RoleId = supervisor.Id, Role = supervisor
        });

        context.Users.Add(user);

        Assert.Equal(2, context.ChangeTracker.Entries<UserRole>().Count());
        Assert.All(context.ChangeTracker.Entries<UserRole>(),
            entry => Assert.Equal(user.Id, entry.Entity.UserId));
        Assert.Single(technician.UserRoles);
        Assert.Single(supervisor.UserRoles);
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Fact]
    public void Dates_and_enums_use_the_intended_PostgreSql_storage_types()
    {
        using var context = CreateContext();
        var properties = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties()).ToArray();
        var timestampProperties = properties
            .Where(property => (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType)
                == typeof(DateTime)).ToArray();
        var dateProperties = properties
            .Where(property => (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(DateOnly)).ToArray();
        var enumProperties = properties
            .Where(property => (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType).IsEnum).ToArray();

        Assert.Equal(52, timestampProperties.Length);
        Assert.All(timestampProperties,
            property => Assert.Equal("timestamp with time zone", property.GetColumnType()));
        Assert.Equal(7, dateProperties.Length);
        Assert.All(dateProperties, property => Assert.Equal("date", property.GetColumnType()));
        Assert.Equal(21, enumProperties.Length);
        Assert.All(enumProperties,
            property => Assert.Equal(typeof(string), property.GetTypeMapping().Converter!.ProviderClrType));
    }

    [Fact]
    public void New_entities_initialize_audit_and_effective_timestamps_in_UTC()
    {
        var user = new User
        {
            EmployeeId = "TEST-001", FirstName = "Test", LastName = "User",
            Email = "test@example.invalid", PasswordHash = "test-only-hash"
        };
        var machine = new Machine { MachineCode = "TEST-M01", Name = "Test machine" };
        var history = new MachineAssignmentHistory();

        Assert.All(new[]
        {
            user.CreatedAt, user.UpdatedAt, machine.CreatedAt,
            machine.UpdatedAt, history.EffectiveFrom
        }, timestamp =>
        {
            Assert.Equal(DateTimeKind.Utc, timestamp.Kind);
            Assert.NotEqual(default, timestamp);
        });
    }

    [Fact]
    public void PostgreSql_create_script_preserves_composite_keys_and_restricts_deletes_without_connecting()
    {
        using var context = CreateContext();

        var script = context.Database.GenerateCreateScript();

        Assert.Contains("CREATE TABLE \"UserRoles\"", script);
        Assert.Contains("PRIMARY KEY (\"UserId\", \"RoleId\")", script);
        Assert.Contains("ON DELETE RESTRICT", script);
        Assert.DoesNotContain("CASCADE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Fact]
    public void Infrastructure_registers_a_scoped_Npgsql_context_using_DefaultConnection_without_connecting()
    {
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration(TestConnectionString);
        services.AddSingleton(configuration);
        services.AddSingleton<ICurrentUser, TestCurrentUser>();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var firstContext = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sameScopeContext = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Same(firstContext, sameScopeContext);
        Assert.NotSame(firstContext, secondContext);
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", firstContext.Database.ProviderName);
        Assert.Equal(TestConnectionString, firstContext.Database.GetConnectionString());
        Assert.Equal(ConnectionState.Closed, firstContext.Database.GetDbConnection().State);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Infrastructure_rejects_a_missing_DefaultConnection(string? connectionString)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(CreateTestConfiguration(connectionString)));

        Assert.Contains("DefaultConnection", exception.Message);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    // Only the unusable model-test setting is loaded; no secrets or external configuration are read.
    private static IConfiguration CreateTestConfiguration(string? connectionString) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString
        }).Build();
}
