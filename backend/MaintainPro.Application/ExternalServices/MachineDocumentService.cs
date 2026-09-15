using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.ExternalServices;

public sealed class MachineDocumentService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock, IFileStorageService storage)
{
    private const int ExpiringSoonDays = 60;

    public async Task<PagedResult<MachineDocumentDto>> ListAsync(Guid machineId, MachineDocumentQuery request,
        CancellationToken ct = default)
    {
        Guard.Page(request.Page, request.PageSize);
        ValidateQuery(request);
        var visibleMachines = ExternalServiceAccess.VisibleMachines(db, currentUser);
        if (!await visibleMachines.AsNoTracking().AnyAsync(x => x.Id == machineId, ct))
            throw new AppException(404, "Machine not found.");
        var query = db.MachineDocuments.AsNoTracking().Where(x => x.MachineId == machineId &&
            visibleMachines.Any(machine => machine.Id == x.MachineId));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = Guard.Required(request.Search, "Search", 200).ToUpperInvariant();
            query = query.Where(x => x.Title.ToUpper().Contains(search));
        }
        if (request.DocumentType.HasValue) query = query.Where(x => x.DocumentType == request.DocumentType);
        if (request.IsActive.HasValue) query = query.Where(x => x.IsActive == request.IsActive);
        if (request.UploadedFrom.HasValue)
        {
            var from = request.UploadedFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(x => x.UploadedAt >= from);
        }
        if (request.UploadedTo.HasValue)
        {
            var to = request.UploadedTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(x => x.UploadedAt <= to);
        }
        var today = Today;
        var soon = today.AddDays(ExpiringSoonDays);
        if (request.ExpiryStatus.HasValue)
            query = request.ExpiryStatus.Value switch
            {
                MachineDocumentExpiryStatus.NO_EXPIRY => query.Where(x => x.ExpiryDate == null),
                MachineDocumentExpiryStatus.EXPIRED => query.Where(x => x.ExpiryDate <= today),
                MachineDocumentExpiryStatus.EXPIRING_SOON => query.Where(x => x.ExpiryDate > today && x.ExpiryDate <= soon),
                MachineDocumentExpiryStatus.VALID => query.Where(x => x.ExpiryDate > soon),
                _ => throw new AppException(400, "ExpiryStatus is invalid.")
            };
        var total = await query.CountAsync(ct);
        var rows = await query.Include(x => x.File).OrderByDescending(x => x.UploadedAt).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return new(rows.Select(ToDto).ToArray(), request.Page, request.PageSize, total);
    }

    public async Task<MachineDocumentDto> GetAsync(Guid machineId, Guid documentId,
        CancellationToken ct = default)
    {
        var machines = ExternalServiceAccess.VisibleMachines(db, currentUser);
        var document = await db.MachineDocuments.AsNoTracking().Include(x => x.File)
            .SingleOrDefaultAsync(x => x.Id == documentId && x.MachineId == machineId &&
                machines.Any(machine => machine.Id == x.MachineId), ct)
            ?? throw new AppException(404, "Machine document not found.");
        return ToDto(document);
    }

    public async Task<MachineDocumentDto> CreateAsync(Guid machineId, MachineDocumentCreateRequest request,
        Stream content, CancellationToken ct = default)
    {
        Validate(request.DocumentType, request.Title, request.DocumentDate, request.ExpiryDate);
        var title = Guard.Required(request.Title, "Title", 300);
        var description = Optional(request.Description, "Description", 2000);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var machine = await ExternalServiceAccess.VisibleMachines(db, currentUser)
            .SingleOrDefaultAsync(x => x.Id == machineId, ct) ?? throw new AppException(404, "Machine not found.");
        ExternalServiceAccess.RequireDocumentManager(currentUser, machine);
        StoredFile? stored = null;
        var commitAttempted = false;
        try
        {
            stored = await storage.StoreAsync(content, request.OriginalFilename, request.MimeType, request.FileSize, ct);
            var now = clock.GetUtcNow().UtcDateTime;
            var file = new FileRecord
            {
                StorageKey = stored.StorageKey, OriginalFilename = stored.OriginalFilename,
                MimeType = stored.MimeType, FileSize = stored.FileSize,
                UploadedByUserId = currentUser.UserId!.Value, UploadedAt = now
            };
            var document = new MachineDocument
            {
                MachineId = machine.Id, FileId = file.Id, File = file,
                DocumentType = request.DocumentType, Title = title, Description = description,
                DocumentDate = request.DocumentDate, ExpiryDate = request.ExpiryDate,
                UploadedByUserId = currentUser.UserId.Value, UploadedAt = now, UpdatedAt = now
            };
            db.FileRecords.Add(file);
            db.MachineDocuments.Add(document);
            audit.Record("MachineDocument.Uploaded", nameof(MachineDocument), document.Id,
                newValues: AuditValues(document, file));
            await db.SaveChangesAsync(ct);
            commitAttempted = true;
            await transaction.CommitAsync(ct);
            return ToDto(document);
        }
        catch
        {
            if (stored is not null && !commitAttempted)
                await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<MachineDocumentDto> UpdateAsync(Guid machineId, Guid documentId,
        MachineDocumentUpdateRequest request, CancellationToken ct = default)
    {
        Validate(request.DocumentType, request.Title, request.DocumentDate, request.ExpiryDate);
        var title = Guard.Required(request.Title, "Title", 300);
        var description = Optional(request.Description, "Description", 2000);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var document = await db.MachineDocuments.Include(x => x.Machine).Include(x => x.File)
            .SingleOrDefaultAsync(x => x.Id == documentId && x.MachineId == machineId, ct)
            ?? throw new AppException(404, "Machine document not found.");
        if (!await ExternalServiceAccess.VisibleMachines(db, currentUser).AnyAsync(x => x.Id == machineId, ct))
            throw new AppException(404, "Machine document not found.");
        ExternalServiceAccess.RequireDocumentManager(currentUser, document.Machine);
        if (!document.IsActive) throw new AppException(409, "Reactivate the machine document before changing its metadata.");
        var oldValues = AuditValues(document, document.File);
        document.DocumentType = request.DocumentType; document.Title = title;
        document.Description = description; document.DocumentDate = request.DocumentDate;
        document.ExpiryDate = request.ExpiryDate; document.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        audit.Record("MachineDocument.Updated", nameof(MachineDocument), document.Id,
            oldValues, AuditValues(document, document.File));
        await db.SaveChangesAsync(ct);
        var result = ToDto(document);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<MachineDocumentDto> SetStatusAsync(Guid machineId, Guid documentId,
        MachineDocumentStatusRequest request, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var document = await db.MachineDocuments.Include(x => x.Machine).Include(x => x.File)
            .SingleOrDefaultAsync(x => x.Id == documentId && x.MachineId == machineId, ct)
            ?? throw new AppException(404, "Machine document not found.");
        if (!await ExternalServiceAccess.VisibleMachines(db, currentUser).AnyAsync(x => x.Id == machineId, ct))
            throw new AppException(404, "Machine document not found.");
        ExternalServiceAccess.RequireDocumentManager(currentUser, document.Machine);
        if (document.IsActive != request.IsActive)
        {
            var old = document.IsActive;
            document.IsActive = request.IsActive;
            document.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            audit.Record("MachineDocument.StatusChanged", nameof(MachineDocument), document.Id,
                new { IsActive = old }, new { document.IsActive });
            await db.SaveChangesAsync(ct);
        }
        var result = ToDto(document);
        await transaction.CommitAsync(ct);
        return result;
    }

    private MachineDocumentDto ToDto(MachineDocument x)
    {
        var days = x.ExpiryDate?.DayNumber - Today.DayNumber;
        var status = x.ExpiryDate is null ? MachineDocumentExpiryStatus.NO_EXPIRY
            : days <= 0 ? MachineDocumentExpiryStatus.EXPIRED
            : days <= ExpiringSoonDays ? MachineDocumentExpiryStatus.EXPIRING_SOON
            : MachineDocumentExpiryStatus.VALID;
        return new(x.Id, x.MachineId, x.FileId, x.DocumentType, x.Title, x.Description,
            x.DocumentDate, x.ExpiryDate, status == MachineDocumentExpiryStatus.EXPIRED,
            days, status == MachineDocumentExpiryStatus.EXPIRING_SOON, status,
            x.File.OriginalFilename, x.File.MimeType, x.File.FileSize,
            x.UploadedByUserId, x.UploadedAt, x.UpdatedAt, x.IsActive);
    }

    private static object AuditValues(MachineDocument x, FileRecord file) => new
    {
        x.MachineId, x.FileId, x.DocumentType, x.Title, x.Description, x.DocumentDate,
        x.ExpiryDate, file.OriginalFilename, file.MimeType, file.FileSize,
        x.UploadedByUserId, x.UploadedAt, x.IsActive
    };

    private static void Validate(MachineDocumentType type, string title,
        DateOnly? documentDate, DateOnly? expiryDate)
    {
        if (!Enum.IsDefined(type)) throw new AppException(400, "DocumentType is invalid.");
        Guard.Required(title, "Title", 300);
        if (documentDate.HasValue && expiryDate < documentDate)
            throw new AppException(400, "ExpiryDate cannot precede DocumentDate.");
    }

    private static void ValidateQuery(MachineDocumentQuery request)
    {
        if (request.DocumentType.HasValue && !Enum.IsDefined(request.DocumentType.Value))
            throw new AppException(400, "DocumentType is invalid.");
        if (request.ExpiryStatus.HasValue && !Enum.IsDefined(request.ExpiryStatus.Value))
            throw new AppException(400, "ExpiryStatus is invalid.");
        if (request.UploadedFrom.HasValue && request.UploadedTo.HasValue && request.UploadedFrom > request.UploadedTo)
            throw new AppException(400, "UploadedFrom cannot follow UploadedTo.");
    }

    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    private static string? Optional(string? value, string name, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : Guard.Required(value, name, max);
}
