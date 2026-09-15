using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<ExternalService> ExternalServices { get; }
    DbSet<ExternalServiceNumberSequence> ExternalServiceNumberSequences { get; }
    DbSet<ExternalServiceAttachment> ExternalServiceAttachments { get; }
    DbSet<MachineDocument> MachineDocuments { get; }
    DbSet<CalibrationCertificate> CalibrationCertificates { get; }
    DbSet<CalibrationRenewal> CalibrationRenewals { get; }
    DbSet<CalibrationNotificationEvent> CalibrationNotificationEvents { get; }
    bool HasActiveTransaction { get; }
    DbSet<Breakdown> Breakdowns { get; }
    DbSet<BreakdownNumberSequence> BreakdownNumberSequences { get; }
    DbSet<BreakdownAssignmentHistory> BreakdownAssignmentHistories { get; }
    DbSet<BreakdownHistoryEvent> BreakdownHistoryEvents { get; }
    DbSet<BreakdownNotificationEvent> BreakdownNotificationEvents { get; }
    DbSet<CorrectiveActionDraft> CorrectiveActionDrafts { get; }
    DbSet<CorrectivePartUsage> CorrectivePartUsages { get; }
    DbSet<BreakdownAttachment> BreakdownAttachments { get; }
    DbSet<CorrectiveSubmission> CorrectiveSubmissions { get; }
    DbSet<CorrectiveSubmissionPartUsage> CorrectiveSubmissionPartUsages { get; }
    DbSet<CorrectiveSubmissionAttachment> CorrectiveSubmissionAttachments { get; }
    DbSet<CorrectiveApproval> CorrectiveApprovals { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationDeliveryAttempt> NotificationDeliveryAttempts { get; }
    DbSet<WorkOrderEscalation> WorkOrderEscalations { get; }
    DbSet<EscalationSettings> EscalationSettings { get; }
    DbSet<NotificationEvent> NotificationEvents { get; }
    DbSet<WorkOrderExecution> WorkOrderExecutions { get; }
    DbSet<WorkOrderChecklistResult> WorkOrderChecklistResults { get; }
    DbSet<SparePartUsage> SparePartUsages { get; }
    DbSet<WorkOrderDefect> WorkOrderDefects { get; }
    DbSet<FileRecord> FileRecords { get; }
    DbSet<WorkOrderAttachment> WorkOrderAttachments { get; }
    DbSet<WorkOrderSubmission> WorkOrderSubmissions { get; }
    DbSet<WorkOrderSubmissionChecklistResult> WorkOrderSubmissionChecklistResults { get; }
    DbSet<WorkOrderSubmissionAttachment> WorkOrderSubmissionAttachments { get; }
    DbSet<WorkOrderSubmissionPartUsage> WorkOrderSubmissionPartUsages { get; }
    DbSet<WorkOrderSubmissionDefect> WorkOrderSubmissionDefects { get; }
    DbSet<WorkOrderApproval> WorkOrderApprovals { get; }
    DbSet<WorkOrderHistoryEvent> WorkOrderHistoryEvents { get; }
    DbSet<Role> Roles { get; }
    DbSet<User> Users { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Department> Departments { get; }
    DbSet<Location> Locations { get; }
    DbSet<MachineCategory> MachineCategories { get; }
    DbSet<Machine> Machines { get; }
    DbSet<MachineAssignmentHistory> MachineAssignmentHistories { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<MaintenanceType> MaintenanceTypes { get; }
    DbSet<MaintenancePlan> MaintenancePlans { get; }
    DbSet<ChecklistTemplate> ChecklistTemplates { get; }
    DbSet<ChecklistItem> ChecklistItems { get; }
    DbSet<WorkOrder> WorkOrders { get; }
    DbSet<WorkOrderDefinition> WorkOrderDefinitions { get; }
    DbSet<WorkOrderChecklistItem> WorkOrderChecklistItems { get; }
    DbSet<WorkOrderNumberSequence> WorkOrderNumberSequences { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public interface IApplicationTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
