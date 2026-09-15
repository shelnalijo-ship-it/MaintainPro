using MaintainPro.Domain.Entities;
using MaintainPro.Application.Abstractions;
using MaintainPro.Domain.Enums;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MaintainPro.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public DbSet<CalibrationCertificate> CalibrationCertificates => Set<CalibrationCertificate>();
    public DbSet<CalibrationRenewal> CalibrationRenewals => Set<CalibrationRenewal>();
    public DbSet<CalibrationNotificationEvent> CalibrationNotificationEvents => Set<CalibrationNotificationEvent>();
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Breakdown> Breakdowns => Set<Breakdown>();
    public DbSet<BreakdownNumberSequence> BreakdownNumberSequences => Set<BreakdownNumberSequence>();
    public DbSet<BreakdownAssignmentHistory> BreakdownAssignmentHistories => Set<BreakdownAssignmentHistory>();
    public DbSet<BreakdownHistoryEvent> BreakdownHistoryEvents => Set<BreakdownHistoryEvent>();
    public DbSet<BreakdownNotificationEvent> BreakdownNotificationEvents => Set<BreakdownNotificationEvent>();
    public DbSet<CorrectiveActionDraft> CorrectiveActionDrafts => Set<CorrectiveActionDraft>();
    public DbSet<CorrectivePartUsage> CorrectivePartUsages => Set<CorrectivePartUsage>();
    public DbSet<BreakdownAttachment> BreakdownAttachments => Set<BreakdownAttachment>();
    public DbSet<CorrectiveSubmission> CorrectiveSubmissions => Set<CorrectiveSubmission>();
    public DbSet<CorrectiveSubmissionPartUsage> CorrectiveSubmissionPartUsages => Set<CorrectiveSubmissionPartUsage>();
    public DbSet<CorrectiveSubmissionAttachment> CorrectiveSubmissionAttachments => Set<CorrectiveSubmissionAttachment>();
    public DbSet<CorrectiveApproval> CorrectiveApprovals => Set<CorrectiveApproval>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDeliveryAttempt> NotificationDeliveryAttempts => Set<NotificationDeliveryAttempt>();
    public DbSet<WorkOrderEscalation> WorkOrderEscalations => Set<WorkOrderEscalation>();
    public DbSet<EscalationSettings> EscalationSettings => Set<EscalationSettings>();
    public DbSet<NotificationEvent> NotificationEvents => Set<NotificationEvent>();
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
            ProtectNotificationHistory(entry);
            ProtectBreakdownHistory(entry);
            ProtectCalibrationHistory(entry);
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
            if (entry.Entity is Machine machine)
            {
                machine.UpdatedAt = DateTime.UtcNow;
                machine.Version = Guid.NewGuid();
                if ((entry.Property(nameof(Machine.Status)).IsModified || entry.Property(nameof(Machine.IsActive)).IsModified)
                    && !entry.Property(nameof(Machine.StatusVersion)).IsModified)
                    machine.StatusVersion = Guid.NewGuid();
            }
            if (entry.Entity is RefreshToken token) token.Version = Guid.NewGuid();
            if (entry.Entity is Notification notification) notification.Version = Guid.NewGuid();
            if (entry.Entity is NotificationEvent notificationEvent) notificationEvent.Version = Guid.NewGuid();
            if (entry.Entity is WorkOrderEscalation escalation) escalation.Version = Guid.NewGuid();
            if (entry.Entity is EscalationSettings settings) settings.Version = Guid.NewGuid();
            if (entry.Entity is MaintenanceType type) { type.UpdatedAt = DateTime.UtcNow; type.Version = Guid.NewGuid(); }
            if (entry.Entity is MaintenancePlan plan) { plan.UpdatedAt = DateTime.UtcNow; plan.Version = Guid.NewGuid(); }
            if (entry.Entity is WorkOrder workOrder) { workOrder.UpdatedAt = DateTime.UtcNow; workOrder.Version = Guid.NewGuid(); }
            if (entry.Entity is WorkOrderNumberSequence sequence) sequence.Version = Guid.NewGuid();
            if (entry.Entity is Breakdown breakdown) { breakdown.UpdatedAt = DateTime.UtcNow; breakdown.Version = Guid.NewGuid(); }
            if (entry.Entity is BreakdownNumberSequence breakdownSequence) breakdownSequence.Version = Guid.NewGuid();
            if (entry.Entity is BreakdownNotificationEvent breakdownEvent) breakdownEvent.Version = Guid.NewGuid();
            if (entry.Entity is CalibrationRenewal renewal) { renewal.UpdatedAt = DateTime.UtcNow; renewal.Version = Guid.NewGuid(); }
            if (entry.Entity is CalibrationNotificationEvent calibrationEvent) calibrationEvent.Version = Guid.NewGuid();
        }
    }

    private static void ProtectCalibrationHistory(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        if (entry.Entity is not (CalibrationCertificate or CalibrationRenewal or CalibrationNotificationEvent)) return;
        if (entry.State == EntityState.Deleted)
            throw new InvalidOperationException("Calibration certificates, renewals and notification events must be retained.");
        if (entry.Entity is CalibrationCertificate && entry.State == EntityState.Modified)
            throw new InvalidOperationException("Calibration certificates are immutable; create a replacement certificate.");
        if (entry.Entity is CalibrationRenewal && entry.State == EntityState.Modified)
        {
            var allowed = new[] { "Status", "CompletedAt", "CompletedCertificateId", "Notes", "UpdatedAt", "Version" };
            if (entry.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name)) ||
                entry.OriginalValues.GetValue<CalibrationRenewalStatus>("Status") != CalibrationRenewalStatus.IN_PROGRESS)
                throw new InvalidOperationException("Completed and cancelled calibration renewals are immutable.");
        }
        if (entry.Entity is CalibrationNotificationEvent && entry.State == EntityState.Modified)
        {
            var allowed = new[] { "ProcessedAt", "LastError", "Version" };
            if (entry.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name)) ||
                entry.OriginalValues.GetValue<DateTime?>("ProcessedAt").HasValue)
                throw new InvalidOperationException("Processed calibration notification events are immutable.");
        }
    }

    private void ProtectBreakdownHistory(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) return;
        if (entry.Entity is CorrectiveSubmission or CorrectiveSubmissionPartUsage or CorrectiveSubmissionAttachment
            or CorrectiveApproval or BreakdownHistoryEvent or BreakdownAssignmentHistory
            && entry.State is EntityState.Modified or EntityState.Deleted)
            throw new InvalidOperationException("Corrective submissions, decisions, assignments and history are immutable.");
        if (entry.Entity is Breakdown or BreakdownNumberSequence or BreakdownNotificationEvent or CorrectiveActionDraft
            or BreakdownAttachment && entry.State == EntityState.Deleted)
            throw new InvalidOperationException("Breakdown records, evidence links and numbering history must be retained.");

        Guid? submissionId = entry.Entity switch
        {
            CorrectiveSubmissionPartUsage x => x.CorrectiveSubmissionId,
            CorrectiveSubmissionAttachment x => x.CorrectiveSubmissionId,
            _ => null
        };
        if (submissionId.HasValue && entry.State == EntityState.Added
            && !ChangeTracker.Entries<CorrectiveSubmission>().Any(x =>
                x.State == EntityState.Added && x.Entity.Id == submissionId.Value))
            throw new InvalidOperationException("Corrective snapshot facts must be created together with their submission.");

        if (entry.Entity is BreakdownNotificationEvent && entry.State == EntityState.Modified)
        {
            var allowed = new[] { "ProcessedAt", "LastError", "Version" };
            if (entry.OriginalValues.GetValue<DateTime?>("ProcessedAt").HasValue ||
                entry.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name)))
                throw new InvalidOperationException("Breakdown notification facts and processed events are immutable.");
        }
        if (entry.Entity is Breakdown && entry.State == EntityState.Modified)
        {
            var reportFacts = new[] { "BreakdownNumber", "MachineId", "MachineCode", "MachineName", "ReportedByUserId",
                "ReporterEmployeeId", "ReporterName", "ReportedAt", "Severity", "MachineStopped", "Description",
                "InitialObservation", "PreviousMachineStatus", "RestoreMachineStatus", "MachineStatusVersionAtStop", "CreatedAt" };
            if (entry.Properties.Any(x => x.IsModified && reportFacts.Contains(x.Metadata.Name)))
                throw new InvalidOperationException("Original breakdown reports and machine stop provenance are immutable.");
            if (entry.OriginalValues.GetValue<DateTime?>("ReturnedToServiceAt").HasValue
                && entry.Property("ReturnedToServiceAt").IsModified)
                throw new InvalidOperationException("A recorded return-to-service time cannot be rewritten.");
            if (entry.OriginalValues.GetValue<BreakdownStatus>("Status") == BreakdownStatus.CLOSED)
            {
                var allowed = new[] { "ReturnedToServiceAt", "HistoryVersion", "UpdatedAt", "Version" };
                if (entry.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name)))
                    throw new InvalidOperationException("Closed breakdowns retain approved facts; only return to service may be recorded.");
            }
        }

        Guid? breakdownId = entry.Entity switch
        {
            CorrectiveActionDraft x => x.BreakdownId,
            CorrectivePartUsage x => x.BreakdownId,
            BreakdownAttachment x => x.BreakdownId,
            _ => null
        };
        if (breakdownId is null) return;
        var tracked = ChangeTracker.Entries<Breakdown>().SingleOrDefault(x => x.Entity.Id == breakdownId);
        var originalStatus = tracked is null
            ? Breakdowns.AsNoTracking().Where(x => x.Id == breakdownId).Select(x => x.Status).Single()
            : tracked.OriginalValues.GetValue<BreakdownStatus>(nameof(Breakdown.Status));
        var starting = tracked is not null && tracked.Entity.Status == BreakdownStatus.IN_PROGRESS
            && originalStatus is BreakdownStatus.ASSIGNED or BreakdownStatus.REJECTED;
        var reportingEvidence = entry.Entity is BreakdownAttachment
            && originalStatus is BreakdownStatus.REPORTED or BreakdownStatus.ASSIGNED;
        if (originalStatus != BreakdownStatus.IN_PROGRESS && !starting && !reportingEvidence)
            throw new InvalidOperationException("Corrective drafts and evidence are locked outside their editable lifecycle states.");
        if (entry.Entity is BreakdownAttachment && entry.State == EntityState.Modified)
        {
            if (entry.Properties.Any(x => x.IsModified && x.Metadata.Name != "IsDeleted") ||
                entry.OriginalValues.GetValue<bool>("IsDeleted"))
                throw new InvalidOperationException("Evidence facts are immutable; active links may only be removed once.");
        }
        if (entry.Entity is CorrectiveActionDraft && entry.State == EntityState.Modified)
        {
            if (entry.Property("BreakdownId").IsModified || entry.Property("CreatedAt").IsModified)
                throw new InvalidOperationException("Corrective draft ownership and creation time are immutable.");
            var identity = new[] { "TechnicianId", "TechnicianEmployeeId", "TechnicianName" };
            if (entry.Properties.Any(x => x.IsModified && identity.Contains(x.Metadata.Name))
                && !(starting && originalStatus == BreakdownStatus.REJECTED))
                throw new InvalidOperationException("Corrective technician identity may only change when resuming a rejected handover.");
        }
        if (entry.Entity is CorrectivePartUsage && entry.State == EntityState.Modified
            && new[] { "BreakdownId", "CreatedByUserId", "CreatedAt" }.Any(x => entry.Property(x).IsModified))
            throw new InvalidOperationException("Corrective part usage ownership and creation facts are immutable.");
    }

    private static void ProtectNotificationHistory(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        if (entry.Entity is not (Notification or NotificationEvent or NotificationDeliveryAttempt or WorkOrderEscalation or MaintainPro.Domain.Entities.EscalationSettings))
            return;
        if (entry.State == EntityState.Deleted)
            throw new InvalidOperationException("Notification and escalation records must be retained.");
        if (entry.State != EntityState.Modified || entry.Entity is MaintainPro.Domain.Entities.EscalationSettings) return;
        var allowed = entry.Entity switch
        {
            Notification => new[] { "ReadAt", "IsRead", "Version" },
            NotificationEvent => new[] { "ProcessedAt", "LastError", "Version" },
            WorkOrderEscalation => new[] { "ResolvedAt", "Version" },
            _ => Array.Empty<string>()
        };
        if (entry.Properties.Any(p => p.IsModified && !allowed.Contains(p.Metadata.Name)))
            throw new InvalidOperationException("Notification payloads, delivery attempts and escalation facts are immutable.");
        if (entry.Entity is Notification && entry.OriginalValues.GetValue<DateTime?>("ReadAt").HasValue
            && entry.Property("ReadAt").IsModified)
            throw new InvalidOperationException("A notification's first read time must be preserved.");
        if (entry.Entity is WorkOrderEscalation && entry.OriginalValues.GetValue<DateTime?>("ResolvedAt").HasValue
            && entry.Property("ResolvedAt").IsModified)
            throw new InvalidOperationException("Resolved escalation history cannot be reopened or rewritten.");
        if (entry.Entity is NotificationEvent && entry.OriginalValues.GetValue<DateTime?>("ProcessedAt").HasValue)
            throw new InvalidOperationException("Processed notification events are immutable.");
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
