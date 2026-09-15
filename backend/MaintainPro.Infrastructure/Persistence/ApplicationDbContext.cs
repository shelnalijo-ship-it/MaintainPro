using MaintainPro.Domain.Entities;
using MaintainPro.Application.Abstractions;
using MaintainPro.Domain.Enums;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MaintainPro.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<WorkOrderExecution> WorkOrderExecutions => Set<WorkOrderExecution>();
    public DbSet<WorkOrderChecklistResult> WorkOrderChecklistResults => Set<WorkOrderChecklistResult>();
    public DbSet<SparePartUsage> SparePartUsages => Set<SparePartUsage>();
    public DbSet<WorkOrderDefect> WorkOrderDefects => Set<WorkOrderDefect>();
    public DbSet<FileRecord> FileRecords => Set<FileRecord>();
    public DbSet<WorkOrderAttachment> WorkOrderAttachments => Set<WorkOrderAttachment>();
    public DbSet<WorkOrderSubmission> WorkOrderSubmissions => Set<WorkOrderSubmission>();
    public DbSet<WorkOrderSubmissionChecklistResult> WorkOrderSubmissionChecklistResults => Set<WorkOrderSubmissionChecklistResult>();
    public DbSet<WorkOrderSubmissionAttachment> WorkOrderSubmissionAttachments => Set<WorkOrderSubmissionAttachment>();
    public DbSet<WorkOrderSubmissionPartUsage> WorkOrderSubmissionPartUsages => Set<WorkOrderSubmissionPartUsage>();
    public DbSet<WorkOrderSubmissionDefect> WorkOrderSubmissionDefects => Set<WorkOrderSubmissionDefect>();
    public DbSet<WorkOrderApproval> WorkOrderApprovals => Set<WorkOrderApproval>();
    public DbSet<WorkOrderHistoryEvent> WorkOrderHistoryEvents => Set<WorkOrderHistoryEvent>();
    public bool HasActiveTransaction => Database.CurrentTransaction is not null;
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<MachineCategory> MachineCategories => Set<MachineCategory>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<MachineAssignmentHistory> MachineAssignmentHistories => Set<MachineAssignmentHistory>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<MaintenanceType> MaintenanceTypes => Set<MaintenanceType>();
    public DbSet<MaintenancePlan> MaintenancePlans => Set<MaintenancePlan>();
    public DbSet<ChecklistTemplate> ChecklistTemplates => Set<ChecklistTemplate>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<WorkOrderDefinition> WorkOrderDefinitions => Set<WorkOrderDefinition>();
    public DbSet<WorkOrderChecklistItem> WorkOrderChecklistItems => Set<WorkOrderChecklistItem>();
    public DbSet<WorkOrderNumberSequence> WorkOrderNumberSequences => Set<WorkOrderNumberSequence>();

    public async Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => new ApplicationTransaction(await Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken));

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareChanges()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries())
        {
            ProtectExecutionHistory(entry);
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is WorkOrder newOrder && !ChangeTracker.Entries<WorkOrderDefinition>()
                    .Any(snapshotEntry => snapshotEntry.State == EntityState.Added && snapshotEntry.Entity.WorkOrderId == newOrder.Id))
                    throw new InvalidOperationException("A work order requires its own definition snapshot.");
                if (entry.Entity is ChecklistItem item && !ChangeTracker.Entries<ChecklistTemplate>()
                    .Any(parent => parent.State == EntityState.Added && parent.Entity.Id == item.ChecklistTemplateId))
                    throw new InvalidOperationException("Items may only be added while creating a new checklist version.");
                if (entry.Entity is WorkOrderChecklistItem snapshotItem && !ChangeTracker.Entries<WorkOrderDefinition>()
                    .Any(parent => parent.State == EntityState.Added && parent.Entity.Id == snapshotItem.WorkOrderDefinitionId))
                    throw new InvalidOperationException("Items may only be added while creating a new work-order definition.");
                if (entry.Entity is WorkOrderDefinition definition && !ChangeTracker.Entries<WorkOrder>()
                    .Any(parent => parent.State == EntityState.Added && parent.Entity.Id == definition.WorkOrderId))
                    throw new InvalidOperationException("A work-order definition must be created with its work order.");
            }
            if (entry.Entity is AuditLog && entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Audit records are append-only.");
            if (entry.Entity is User or Machine && entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Users and machines must be deactivated rather than deleted.");
            if (entry.Entity is ChecklistTemplate or ChecklistItem or WorkOrderDefinition or WorkOrderChecklistItem
                && entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Checklist versions and work-order definitions are immutable. Create a new version.");
            if (entry.Entity is MaintenanceType or MaintenancePlan or WorkOrder && entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Maintenance records must be retained for history.");
            if (entry.State != EntityState.Modified) continue;
            if (entry.Entity is User user) { user.UpdatedAt = DateTime.UtcNow; user.Version = Guid.NewGuid(); }
            if (entry.Entity is Machine machine) { machine.UpdatedAt = DateTime.UtcNow; machine.Version = Guid.NewGuid(); }
            if (entry.Entity is RefreshToken token) token.Version = Guid.NewGuid();
            if (entry.Entity is MaintenanceType type) { type.UpdatedAt = DateTime.UtcNow; type.Version = Guid.NewGuid(); }
            if (entry.Entity is MaintenancePlan plan) { plan.UpdatedAt = DateTime.UtcNow; plan.Version = Guid.NewGuid(); }
            if (entry.Entity is WorkOrder workOrder) { workOrder.UpdatedAt = DateTime.UtcNow; workOrder.Version = Guid.NewGuid(); }
            if (entry.Entity is WorkOrderNumberSequence sequence) sequence.Version = Guid.NewGuid();
        }
    }

    private void ProtectExecutionHistory(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) return;
        if (entry.Entity is WorkOrderSubmission or WorkOrderSubmissionChecklistResult
            or WorkOrderSubmissionAttachment or WorkOrderSubmissionPartUsage or WorkOrderSubmissionDefect
            or WorkOrderApproval or WorkOrderHistoryEvent or FileRecord
            && entry.State is EntityState.Modified or EntityState.Deleted)
            throw new InvalidOperationException("Submitted facts, decisions, file metadata and history are immutable.");

        Guid? submissionId = entry.Entity switch
        {
            WorkOrderSubmissionChecklistResult x => x.WorkOrderSubmissionId,
            WorkOrderSubmissionAttachment x => x.WorkOrderSubmissionId,
            WorkOrderSubmissionPartUsage x => x.WorkOrderSubmissionId,
            WorkOrderSubmissionDefect x => x.WorkOrderSubmissionId,
            _ => null
        };
        if (submissionId.HasValue && entry.State == EntityState.Added
            && !ChangeTracker.Entries<WorkOrderSubmission>().Any(x =>
                x.State == EntityState.Added && x.Entity.Id == submissionId.Value))
            throw new InvalidOperationException("Submission facts must be created together with their submission.");

        if (entry.Entity is WorkOrder && entry.State == EntityState.Modified
            && entry.OriginalValues.GetValue<WorkOrderLifecycleStatus>(nameof(WorkOrder.LifecycleStatus))
                == WorkOrderLifecycleStatus.APPROVED)
            throw new InvalidOperationException("Approved work orders are immutable.");

        Guid? orderId = entry.Entity switch
        {
            WorkOrderExecution x => x.WorkOrderId,
            WorkOrderChecklistResult x => x.WorkOrderId,
            SparePartUsage x => x.WorkOrderId,
            WorkOrderDefect x => x.WorkOrderId,
            WorkOrderAttachment x => x.WorkOrderId,
            _ => null
        };
        if (orderId is null) return;
        var tracked = ChangeTracker.Entries<WorkOrder>().SingleOrDefault(x => x.Entity.Id == orderId);
        var status = tracked is null
            ? WorkOrders.AsNoTracking().Where(x => x.Id == orderId).Select(x => x.LifecycleStatus).Single()
            : tracked.OriginalValues.GetValue<WorkOrderLifecycleStatus>(nameof(WorkOrder.LifecycleStatus));
        // Start and resume create/update the draft in the same transaction as the transition.
        var starting = tracked is not null && tracked.Entity.LifecycleStatus == WorkOrderLifecycleStatus.IN_PROGRESS
            && status is WorkOrderLifecycleStatus.PLANNED or WorkOrderLifecycleStatus.ASSIGNED or WorkOrderLifecycleStatus.REJECTED;
        if (status != WorkOrderLifecycleStatus.IN_PROGRESS && !starting)
            throw new InvalidOperationException("Execution drafts can only change while work is in progress.");
    }

    private sealed class ApplicationTransaction(IDbContextTransaction transaction) : IApplicationTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
