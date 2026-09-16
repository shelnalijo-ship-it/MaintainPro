namespace MaintainPro.Application.Audit;

public sealed record AuditLogQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? UserId = null,
    string? Action = null,
    string? EntityType = null,
    string? EntityId = null,
    int Page = 1,
    int PageSize = 20);

public sealed record AuditLogUserSummaryDto(
    Guid Id,
    string EmployeeId,
    string FirstName,
    string LastName,
    string Email);

public sealed record AuditLogDto(
    Guid Id,
    DateTime CreatedAt,
    Guid? UserId,
    AuditLogUserSummaryDto? User,
    string Action,
    string EntityType,
    string? EntityId,
    string? OldValuesJson,
    string? NewValuesJson,
    string? IpAddress,
    string? DeviceInfo);
