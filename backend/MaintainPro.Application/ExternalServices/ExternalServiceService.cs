using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.ExternalServices;

public sealed class ExternalServiceService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock, ExternalServiceNumberAllocator numbers,
    IGenerationConcurrency concurrency)
{
    public async Task<PagedResult<ExternalServiceDto>> ListAsync(ExternalServiceQuery request,
        CancellationToken ct = default)
    {
        Guard.Page(request.Page, request.PageSize);
        ValidateQuery(request);
        var query = ExternalServiceAccess.VisibleServices(db, currentUser).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = Guard.Required(request.Search, "Search", 200).ToUpperInvariant();
            query = query.Where(x => x.ServiceNumber.ToUpper().Contains(search) ||
                x.MachineCode.ToUpper().Contains(search) || x.MachineName.ToUpper().Contains(search) ||
                x.ServiceCompany.ToUpper().Contains(search) ||
                (x.ServiceTechnician != null && x.ServiceTechnician.ToUpper().Contains(search)) ||
                (x.PurchaseOrderNumber != null && x.PurchaseOrderNumber.ToUpper().Contains(search)) ||
                (x.InvoiceNumber != null && x.InvoiceNumber.ToUpper().Contains(search)));
        }
        if (request.MachineId.HasValue) query = query.Where(x => x.MachineId == request.MachineId);
        if (!string.IsNullOrWhiteSpace(request.ServiceCompany))
        {
            var company = request.ServiceCompany.Trim().ToUpperInvariant();
            query = query.Where(x => x.ServiceCompany.ToUpper().Contains(company));
        }
        if (request.ServiceType.HasValue) query = query.Where(x => x.ServiceType == request.ServiceType);
        if (request.ServiceFrom.HasValue) query = query.Where(x => x.ServiceDate >= request.ServiceFrom);
        if (request.ServiceTo.HasValue) query = query.Where(x => x.ServiceDate <= request.ServiceTo);
        if (request.FollowUpFrom.HasValue) query = query.Where(x => x.FollowUpDate >= request.FollowUpFrom);
        if (request.FollowUpTo.HasValue) query = query.Where(x => x.FollowUpDate <= request.FollowUpTo);
        if (request.NextServiceFrom.HasValue) query = query.Where(x => x.NextServiceDate >= request.NextServiceFrom);
        if (request.NextServiceTo.HasValue) query = query.Where(x => x.NextServiceDate <= request.NextServiceTo);
        if (request.HasAttachments.HasValue)
            query = query.Where(x => x.Attachments.Any(a => a.IsActive) == request.HasAttachments.Value);
        if (!string.IsNullOrWhiteSpace(request.PurchaseOrderNumber))
        {
            var value = request.PurchaseOrderNumber.Trim().ToUpperInvariant();
            query = query.Where(x => x.PurchaseOrderNumber != null && x.PurchaseOrderNumber.ToUpper().Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(request.InvoiceNumber))
        {
            var value = request.InvoiceNumber.Trim().ToUpperInvariant();
            query = query.Where(x => x.InvoiceNumber != null && x.InvoiceNumber.ToUpper().Contains(value));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.ServiceDate).ThenByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new { Service = x, ActiveAttachments = x.Attachments.Count(a => a.IsActive) }).ToListAsync(ct);
        return new(rows.Select(x => ToDto(x.Service, x.ActiveAttachments)).ToArray(),
            request.Page, request.PageSize, total);
    }

    public async Task<ExternalServiceDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var visible = ExternalServiceAccess.VisibleServices(db, currentUser);
        var row = await visible.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { Service = x, ActiveAttachments = x.Attachments.Count(a => a.IsActive) })
            .SingleOrDefaultAsync(ct) ?? throw new AppException(404, "External service not found.");
        return ToDto(row.Service, row.ActiveAttachments);
    }

    public async Task<PagedResult<ExternalServiceDto>> MachineHistoryAsync(Guid machineId,
        ExternalServiceQuery request, CancellationToken ct = default)
    {
        if (!await ExternalServiceAccess.VisibleMachines(db, currentUser).AsNoTracking()
                .AnyAsync(x => x.Id == machineId, ct))
            throw new AppException(404, "Machine not found.");
        return await ListAsync(request with { MachineId = machineId }, ct);
    }

    public Task<ExternalServiceDto> CreateAsync(ExternalServiceWriteRequest request,
        CancellationToken ct = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Technician, RoleNames.Supervisor, RoleNames.Manager, RoleNames.Admin);
        var value = Validate(request);
        if (!ExternalServiceAccess.CanManageAll(currentUser) && !currentUser.Roles.Contains(RoleNames.Supervisor) &&
            (value.Cost.HasValue || value.PurchaseOrderNumber is not null || value.InvoiceNumber is not null))
            throw new AppException(403, "Technician entries cannot administer cost, purchase-order, or invoice fields.");
        return WriteAsync(async () =>
        {
            await using var transaction = await db.BeginTransactionAsync(ct);
            var machine = await ExternalServiceAccess.VisibleMachines(db, currentUser)
                .SingleOrDefaultAsync(x => x.Id == value.MachineId, ct)
                ?? throw new AppException(404, "Machine not found.");
            var now = clock.GetUtcNow().UtcDateTime;
            var service = new ExternalService
            {
                ServiceNumber = await numbers.AllocateAsync(now, ct), MachineId = machine.Id,
                MachineCode = machine.MachineCode, MachineName = machine.Name,
                ServiceCompany = value.ServiceCompany, Description = value.Description,
                CreatedByUserId = currentUser.UserId!.Value, CreatedAt = now, UpdatedAt = now
            };
            Apply(service, value);
            db.ExternalServices.Add(service);
            audit.Record("ExternalService.Created", nameof(ExternalService), service.Id,
                newValues: AuditValues(service));
            await db.SaveChangesAsync(ct);
            var result = ToDto(service, 0);
            await transaction.CommitAsync(ct);
            return result;
        }, ct);
    }

    public Task<ExternalServiceDto> UpdateAsync(Guid id, ExternalServiceWriteRequest request,
        CancellationToken ct = default)
    {
        var value = Validate(request);
        return WriteAsync(async () =>
        {
            await using var transaction = await db.BeginTransactionAsync(ct);
            var service = await ExternalServiceAccess.VisibleServices(db, currentUser).Include(x => x.Machine)
                .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException(404, "External service not found.");
            ExternalServiceAccess.RequireServiceEditor(currentUser, service);
            if (value.MachineId != service.MachineId)
                throw new AppException(409, "A historical external service cannot be moved to another machine.");
            var oldValues = AuditValues(service);
            Apply(service, value);
            service.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            audit.Record("ExternalService.Corrected", nameof(ExternalService), service.Id,
                oldValues, AuditValues(service));
            await db.SaveChangesAsync(ct);
            var count = await db.ExternalServiceAttachments.CountAsync(x => x.ExternalServiceId == id && x.IsActive, ct);
            var result = ToDto(service, count);
            await transaction.CommitAsync(ct);
            return result;
        }, ct);
    }

    public async Task<PagedResult<ExternalServiceFollowUpDto>> FollowUpsAsync(
        ExternalServiceFollowUpQuery request, CancellationToken ct = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Supervisor, RoleNames.Manager, RoleNames.Admin);
        Guard.Page(request.Page, request.PageSize);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var query = ExternalServiceAccess.VisibleServices(db, currentUser).AsNoTracking()
            .Where(x => x.FollowUpDate.HasValue || x.NextServiceDate.HasValue);
        if (request.MachineId.HasValue) query = query.Where(x => x.MachineId == request.MachineId);
        if (!string.IsNullOrWhiteSpace(request.ServiceCompany))
        {
            var company = request.ServiceCompany.Trim().ToUpperInvariant();
            query = query.Where(x => x.ServiceCompany.ToUpper().Contains(company));
        }
        if (request.DueBefore.HasValue)
            query = query.Where(x => x.FollowUpDate <= request.DueBefore || x.NextServiceDate <= request.DueBefore);
        if (request.OverdueOnly)
            query = query.Where(x => x.FollowUpDate < today || x.NextServiceDate < today);
        var total = await query.CountAsync(ct);
        var rows = await query.Include(x => x.Machine)
            .OrderBy(x => x.FollowUpDate ?? x.NextServiceDate).ThenBy(x => x.ServiceNumber)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return new(rows.Select(x => ToFollowUp(x, today)).ToArray(), request.Page, request.PageSize, total);
    }

    private async Task<T> WriteAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (db.HasActiveTransaction) throw new InvalidOperationException("External-service commands must own their transactions.");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            concurrency.ResetTracking();
            try { return await action(); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception) when (concurrency.IsRetryable(exception))
            {
                if (attempt == 4) throw new AppException(409, "Concurrent external-service changes prevented completion. Retry the command.");
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), ct);
            }
            finally { concurrency.ResetTracking(); }
        }
        throw new InvalidOperationException("The bounded external-service retry loop exited unexpectedly.");
    }

    private ExternalServiceWriteRequest Validate(ExternalServiceWriteRequest request)
    {
        if (!Enum.IsDefined(request.ServiceType)) throw new AppException(400, "ServiceType is invalid.");
        if (request.Cost < 0) throw new AppException(400, "Cost cannot be negative.");
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if (request.ServiceDate > today) throw new AppException(400, "ServiceDate cannot be in the future.");
        if (request.FollowUpDate < request.ServiceDate) throw new AppException(400, "FollowUpDate cannot precede ServiceDate.");
        if (request.NextServiceDate < request.ServiceDate) throw new AppException(400, "NextServiceDate cannot precede ServiceDate.");
        return request with
        {
            ServiceCompany = Guard.Required(request.ServiceCompany, "ServiceCompany", 300),
            ServiceTechnician = Optional(request.ServiceTechnician, "ServiceTechnician", 200),
            Description = Guard.Required(request.Description, "Description", 10000),
            Findings = Optional(request.Findings, "Findings", 10000),
            WorkCompleted = Optional(request.WorkCompleted, "WorkCompleted", 10000),
            PartsReplaced = Optional(request.PartsReplaced, "PartsReplaced", 10000),
            PurchaseOrderNumber = Optional(request.PurchaseOrderNumber, "PurchaseOrderNumber", 200),
            InvoiceNumber = Optional(request.InvoiceNumber, "InvoiceNumber", 200),
            Recommendation = Optional(request.Recommendation, "Recommendation", 10000),
            Comments = Optional(request.Comments, "Comments", 10000)
        };
    }

    private static void Apply(ExternalService target, ExternalServiceWriteRequest source)
    {
        target.ServiceCompany = source.ServiceCompany; target.ServiceTechnician = source.ServiceTechnician;
        target.ServiceDate = source.ServiceDate; target.ServiceType = source.ServiceType;
        target.Description = source.Description; target.Findings = source.Findings;
        target.WorkCompleted = source.WorkCompleted; target.PartsReplaced = source.PartsReplaced;
        target.Cost = source.Cost; target.PurchaseOrderNumber = source.PurchaseOrderNumber;
        target.InvoiceNumber = source.InvoiceNumber; target.FollowUpDate = source.FollowUpDate;
        target.NextServiceDate = source.NextServiceDate; target.Recommendation = source.Recommendation;
        target.Comments = source.Comments;
    }

    private static object AuditValues(ExternalService x) => new
    {
        x.ServiceNumber, x.MachineId, x.MachineCode, x.MachineName, x.ServiceCompany,
        x.ServiceTechnician, x.ServiceDate, x.ServiceType, x.Description, x.Findings,
        x.WorkCompleted, x.PartsReplaced, x.Cost, x.PurchaseOrderNumber, x.InvoiceNumber,
        x.FollowUpDate, x.NextServiceDate, x.Recommendation, x.Comments,
        x.CreatedByUserId, x.CreatedAt, x.UpdatedAt
    };

    private static ExternalServiceDto ToDto(ExternalService x, int attachmentCount) => new(x.Id,
        x.ServiceNumber, x.MachineId, x.MachineCode, x.MachineName, x.ServiceCompany,
        x.ServiceTechnician, x.ServiceDate, x.ServiceType, x.Description, x.Findings,
        x.WorkCompleted, x.PartsReplaced, x.Cost, x.PurchaseOrderNumber, x.InvoiceNumber,
        x.FollowUpDate, x.NextServiceDate, x.Recommendation, x.Comments, x.CreatedByUserId,
        x.CreatedAt, x.UpdatedAt, attachmentCount);

    private static ExternalServiceFollowUpDto ToFollowUp(ExternalService x, DateOnly today)
    {
        var due = new[] { x.FollowUpDate, x.NextServiceDate }.Where(d => d.HasValue)
            .Select(d => d!.Value).Min();
        return new(x.Id, x.ServiceNumber, x.MachineId, x.MachineCode, x.MachineName,
            x.ServiceCompany, x.FollowUpDate, x.NextServiceDate, due, due < today,
            x.Recommendation, x.Machine.MachineOwnerUserId, x.Machine.SupervisorUserId);
    }

    private static string? Optional(string? value, string name, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : Guard.Required(value, name, max);

    private static void ValidateQuery(ExternalServiceQuery q)
    {
        if (q.ServiceType.HasValue && !Enum.IsDefined(q.ServiceType.Value)) throw new AppException(400, "ServiceType is invalid.");
        ValidateRange(q.ServiceFrom, q.ServiceTo, "Service");
        ValidateRange(q.FollowUpFrom, q.FollowUpTo, "FollowUp");
        ValidateRange(q.NextServiceFrom, q.NextServiceTo, "NextService");
    }

    private static void ValidateRange(DateOnly? from, DateOnly? to, string name)
    {
        if (from.HasValue && to.HasValue && from > to)
            throw new AppException(400, $"{name}From cannot follow {name}To.");
    }
}
