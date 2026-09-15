using MaintainPro.Domain.Entities;
using MaintainPro.Application.Abstractions;
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
