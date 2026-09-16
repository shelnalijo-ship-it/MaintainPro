using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Audit;

public sealed class AuditLogQueryService(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<PagedResult<AuditLogDto>> ListAsync(AuditLogQuery request,
        CancellationToken cancellationToken = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Manager, RoleNames.Admin);
        Guard.Page(request.Page, request.PageSize);
        if (request.From.HasValue && request.To.HasValue && request.From > request.To)
            throw new AppException(400, "From must be on or before To.");

        var action = OptionalFilter(request.Action, "Action");
        var entityType = OptionalFilter(request.EntityType, "Entity type");
        var entityId = OptionalFilter(request.EntityId, "Entity ID");
        var query = db.AuditLogs.AsNoTracking();

        if (request.From.HasValue)
        {
            var from = DateTime.SpecifyKind(request.From.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(log => log.CreatedAt >= from);
        }
        if (request.To.HasValue)
        {
            var through = DateTime.SpecifyKind(request.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(log => log.CreatedAt < through);
        }
        if (request.UserId.HasValue) query = query.Where(log => log.UserId == request.UserId);
        if (action is not null) query = query.Where(log => log.Action == action);
        if (entityType is not null) query = query.Where(log => log.EntityType == entityType);
        if (entityId is not null) query = query.Where(log => log.EntityId == entityId);

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(log => log.CreatedAt).ThenByDescending(log => log.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(log => new
            {
                log.Id, log.CreatedAt, log.UserId, log.Action, log.EntityType, log.EntityId,
                log.OldValuesJson, log.NewValuesJson, log.IpAddress, log.DeviceInfo,
                DisplayUserId = log.User == null ? null : (Guid?)log.User.Id,
                EmployeeId = log.User == null ? null : log.User.EmployeeId,
                FirstName = log.User == null ? null : log.User.FirstName,
                LastName = log.User == null ? null : log.User.LastName,
                Email = log.User == null ? null : log.User.Email
            }).ToListAsync(cancellationToken);

        var items = rows.Select(row => new AuditLogDto(
            row.Id,
            row.CreatedAt,
            row.UserId,
            row.DisplayUserId.HasValue
                ? new AuditLogUserSummaryDto(row.DisplayUserId.Value, row.EmployeeId!, row.FirstName!, row.LastName!, row.Email!)
                : null,
            row.Action,
            row.EntityType,
            row.EntityId,
            AuditJsonSanitizer.Sanitize(row.OldValuesJson),
            AuditJsonSanitizer.Sanitize(row.NewValuesJson),
            AuditJsonSanitizer.SanitizeMetadata(row.IpAddress, 128),
            AuditJsonSanitizer.SanitizeMetadata(row.DeviceInfo, 512))).ToArray();

        return new PagedResult<AuditLogDto>(items, request.Page, request.PageSize, total);
    }

    private static string? OptionalFilter(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > 200) throw new AppException(400, $"{name} must not exceed 200 characters.");
        return trimmed;
    }
}
