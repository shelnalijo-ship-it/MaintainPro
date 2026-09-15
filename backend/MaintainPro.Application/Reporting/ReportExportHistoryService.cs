using System.Text.Json;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Reporting;

public sealed class ReportExportHistoryService(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<PagedResult<ReportExportHistoryDto>> ListAsync(ReportExportHistoryQuery request,
        CancellationToken ct = default)
    {
        ReportingAccess.RequireReportAccess(currentUser);
        Guard.Page(request.Page, request.PageSize);
        if (request.From > request.To) throw new AppException(400, "From date cannot follow To date.");
        var boundsFrom = request.From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var boundsTo = request.To?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var query = db.AuditLogs.AsNoTracking().Where(x => x.Action == "Report.Exported" &&
            x.EntityType == "Report" && x.UserId.HasValue && x.NewValuesJson != null);
        if (!ReportingAccess.IsManagement(currentUser))
        {
            var actorId = Guard.Authenticated(currentUser);
            query = query.Where(x => x.UserId == actorId);
        }
        if (request.GeneratedByUserId.HasValue)
            query = query.Where(x => x.UserId == request.GeneratedByUserId);
        if (boundsFrom.HasValue) query = query.Where(x => x.CreatedAt >= boundsFrom);
        if (boundsTo.HasValue) query = query.Where(x => x.CreatedAt < boundsTo);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new { x.Id, UserId = x.UserId!.Value,
                User = x.User == null ? null : x.User.EmployeeId + " - " + x.User.FirstName + " " + x.User.LastName,
                x.CreatedAt, x.NewValuesJson }).ToListAsync(ct);
        var items = rows.Select(x =>
        {
            var payload = JsonSerializer.Deserialize<ExportAuditPayload>(x.NewValuesJson!)
                ?? throw new InvalidOperationException("Report export audit payload is invalid.");
            return new ReportExportHistoryDto(x.Id, payload.ReportName, payload.Format,
                payload.RowCount, payload.FileName, payload.FilterSummary, x.UserId,
                x.User ?? x.UserId.ToString(), x.CreatedAt);
        }).ToArray();
        return new(items, request.Page, request.PageSize, total);
    }

    private sealed record ExportAuditPayload(string ReportName, string Format, int RowCount,
        string FileName, DateTime GeneratedAt, Guid GeneratedByUserId, string FilterSummary);
}
