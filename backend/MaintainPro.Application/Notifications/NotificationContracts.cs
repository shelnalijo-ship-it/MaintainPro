using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Notifications;

public sealed record NotificationQuery(bool? IsRead = null, NotificationType? Type = null,
    NotificationPriority? Priority = null, DateOnly? From = null, DateOnly? To = null,
    int Page = 1, int PageSize = 20);

public sealed record NotificationDto(Guid Id, Guid UserId, NotificationType NotificationType,
    string Title, string Message, string? EntityType, Guid? EntityId, DateTime CreatedAt,
    DateTime? ReadAt, bool IsRead, NotificationPriority Priority, DateTime? ExpiresAt);

public sealed record NotificationUnreadCountDto(int UnreadCount);
public sealed record NotificationReadAllDto(int UpdatedCount);

public sealed record NotificationWriteRequest(Guid UserId, NotificationType NotificationType,
    string Title, string Message, NotificationPriority Priority, string DeduplicationKey,
    string? EntityType = null, Guid? EntityId = null, DateTime? ExpiresAt = null);
public sealed record NotificationWriteResult(Guid NotificationId, bool Created);

public sealed record EscalationSettingsRequest(int DueSoonDays, int TechnicianOverdueDays,
    int SupervisorEscalationDays, int ManagerEscalationDays);
public sealed record EscalationSettingsDto(int DueSoonDays, int TechnicianOverdueDays,
    int SupervisorEscalationDays, int ManagerEscalationDays, DateTime? UpdatedAt, Guid? UpdatedByUserId);

public sealed record EscalationQuery(Guid? WorkOrderId = null, Guid? TechnicianId = null,
    Guid? SupervisorId = null, int? Level = null, bool? UnresolvedOnly = null,
    DateOnly? From = null, DateOnly? To = null, int Page = 1, int PageSize = 20);
public sealed record EscalationDto(Guid Id, Guid WorkOrderId, string WorkOrderNumber,
    Guid MachineId, string MachineCode, string MachineName, Guid? TechnicianId,
    Guid SupervisorId, int Level, NotificationType TriggerType, DateTime TriggeredAt,
    Guid RecipientUserId, Guid NotificationId, DateTime? ResolvedAt, string? RoutingReason);

public sealed record NotificationDispatchResult(int NotificationsCreated, int EscalationsCreated,
    int DuplicatesSkipped, int Errors, IReadOnlyList<string> Issues)
{
    public static NotificationDispatchResult Empty { get; } = new(0, 0, 0, 0, Array.Empty<string>());
    public NotificationDispatchResult Add(NotificationDispatchResult other) => new(
        NotificationsCreated + other.NotificationsCreated, EscalationsCreated + other.EscalationsCreated,
        DuplicatesSkipped + other.DuplicatesSkipped, Errors + other.Errors,
        Issues.Concat(other.Issues).ToArray());
}

public sealed record NotificationRecipients(IReadOnlyList<Guid> UserIds, string? RoutingReason,
    bool IsComplete = true);
