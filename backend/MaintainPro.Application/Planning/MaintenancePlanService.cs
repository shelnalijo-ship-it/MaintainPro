using System.Linq.Expressions;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Planning;

public sealed class MaintenancePlanService(
    IApplicationDbContext db, ICurrentUser currentUser, IAuditWriter audit, TimeProvider clock,
    RecurrenceService recurrence)
{
    private static readonly Expression<Func<MaintenancePlan, MaintenancePlanDto>> Projection = plan => new(
        plan.Id, plan.MachineId, plan.PlanName, plan.MaintenanceTypeId, plan.Priority,
        plan.FrequencyType, plan.FrequencyValue, plan.StartDate, plan.NextDueDate,
        plan.DefaultTechnicianId, plan.SupervisorId, plan.Instructions, plan.EstimatedDurationMinutes,
        plan.PhotoRequired, plan.MinimumPhotoCount, plan.CommentRequired, plan.IsActive,
        plan.CreatedByUserId, plan.CreatedAt, plan.UpdatedAt);

    public async Task<PagedResult<MaintenancePlanDto>> ListAsync(PlanQuery request, CancellationToken ct = default)
    {
        Guard.Page(request.Page, request.PageSize);
        var query = VisiblePlans().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = Guard.Required(request.Search, "Search").ToUpperInvariant();
            query = query.Where(plan => plan.PlanName.ToUpper().Contains(search)
                || plan.Machine.MachineCode.ToUpper().Contains(search)
                || plan.Machine.Name.ToUpper().Contains(search));
        }
        if (request.MachineId.HasValue) query = query.Where(x => x.MachineId == request.MachineId);
        if (request.MaintenanceTypeId.HasValue) query = query.Where(x => x.MaintenanceTypeId == request.MaintenanceTypeId);
        if (request.SupervisorId.HasValue) query = query.Where(x => x.SupervisorId == request.SupervisorId);
        if (request.DefaultTechnicianId.HasValue) query = query.Where(x => x.DefaultTechnicianId == request.DefaultTechnicianId);
        if (request.IsActive.HasValue) query = query.Where(x => x.IsActive == request.IsActive);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.NextDueDate).ThenBy(x => x.PlanName).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(Projection).ToListAsync(ct);
        return new(items, request.Page, request.PageSize, total);
    }

    public async Task<MaintenancePlanDto> GetAsync(Guid id, CancellationToken ct = default) =>
        await VisiblePlans().AsNoTracking().Where(x => x.Id == id).Select(Projection).SingleOrDefaultAsync(ct)
        ?? throw new AppException(404, "Maintenance plan not found.");

    public async Task<MaintenancePlanDto> CreateAsync(PlanWriteRequest request, CancellationToken ct = default)
    {
        RequireManager();
        var normalized = ValidatePlan(request);
        await using var transaction = await db.BeginTransactionAsync(ct);
        await ValidateReferencesAsync(normalized, ct);
        var plan = NewPlan(normalized);
        db.MaintenancePlans.Add(plan);
        audit.Record("maintenance_plan.created", nameof(MaintenancePlan), plan.Id, newValues: ToDto(plan));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(plan);
    }

    public async Task<MaintenancePlanDto> UpdateAsync(Guid id, PlanWriteRequest request, CancellationToken ct = default)
    {
        RequireManager();
        var normalized = ValidatePlan(request);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var plan = await FindAsync(id, ct);
        await ValidateReferencesAsync(normalized, ct);
        var previous = ToDto(plan);
        var recurrenceChanged = plan.StartDate != normalized.StartDate
            || plan.FrequencyType != normalized.FrequencyType || plan.FrequencyValue != normalized.FrequencyValue;
        if (recurrenceChanged)
        {
            // A changed schedule begins with future work. Generated occurrences, including
            // cancelled ones, keep their original dates and their persistent occurrence keys.
            var notBefore = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            var latestOccurrence = await db.WorkOrders.AsNoTracking().Where(x => x.MaintenancePlanId == id)
                .OrderByDescending(x => x.PlannedDate).Select(x => (DateOnly?)x.PlannedDate).FirstOrDefaultAsync(ct);
            if (latestOccurrence.HasValue)
            {
                if (latestOccurrence.Value == DateOnly.MaxValue)
                    throw new AppException(409, "No supported calendar date remains after the last generated occurrence.");
                var afterLast = latestOccurrence.Value.AddDays(1);
                if (afterLast > notBefore) notBefore = afterLast;
            }
            plan.NextDueDate = recurrence.OnOrAfter(normalized.StartDate, notBefore,
                normalized.FrequencyType, normalized.FrequencyValue);
        }
        ApplyPlanFields(plan, normalized);
        plan.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        audit.Record("maintenance_plan.updated", nameof(MaintenancePlan), id, previous, ToDto(plan));
        if (previous.IsActive != plan.IsActive)
            audit.Record("maintenance_plan.status_changed", nameof(MaintenancePlan), id,
                new { previous.IsActive }, new { plan.IsActive });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(plan);
    }

    public async Task<MaintenancePlanDto> DuplicateAsync(Guid id, DuplicatePlanRequest request,
        CancellationToken ct = default)
    {
        RequireManager();
        var name = Guard.Required(request.PlanName, "PlanName");
        await using var transaction = await db.BeginTransactionAsync(ct);
        var source = await FindAsync(id, ct);
        var copyRequest = ValidatePlan(new PlanWriteRequest(
            request.MachineId ?? source.MachineId, name, source.MaintenanceTypeId, source.Priority,
            source.FrequencyType, source.FrequencyValue, request.StartDate, source.SupervisorId,
            source.DefaultTechnicianId, source.Instructions, source.EstimatedDurationMinutes,
            source.PhotoRequired, source.MinimumPhotoCount, source.CommentRequired, source.IsActive));
        await ValidateReferencesAsync(copyRequest, ct);
        var copy = NewPlan(copyRequest);
        db.MaintenancePlans.Add(copy);

        var sourceChecklist = await db.ChecklistTemplates.AsNoTracking().Include(x => x.Items)
            .Where(x => x.MaintenancePlanId == id && x.IsActive).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (sourceChecklist is not null)
        {
            var copiedDefinition = ValidateChecklist(new ChecklistWriteRequest(sourceChecklist.Name,
                sourceChecklist.Items.OrderBy(x => x.SequenceNumber).Select(x => new ChecklistItemWriteRequest(
                    x.SequenceNumber, x.Title, x.ResponseType, x.IsMandatory, x.Description,
                    x.Unit, x.MinimumValue, x.MaximumValue, x.PhotoRequired)).ToArray()));
            var template = NewTemplate(copy.Id, 1, copiedDefinition);
            db.ChecklistTemplates.Add(template);
            audit.Record("checklist.version_created", nameof(ChecklistTemplate), template.Id, newValues: ToDto(template));
        }
        audit.Record("maintenance_plan.created", nameof(MaintenancePlan), copy.Id, newValues: ToDto(copy));
        audit.Record("maintenance_plan.duplicated", nameof(MaintenancePlan), copy.Id,
            new { SourcePlanId = id }, ToDto(copy));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(copy);
    }

    public async Task<MaintenancePlanDto> SetStatusAsync(Guid id, PlanStatusRequest request,
        CancellationToken ct = default)
    {
        RequireManager();
        await using var transaction = await db.BeginTransactionAsync(ct);
        var plan = await FindAsync(id, ct);
        if (plan.IsActive != request.IsActive)
        {
            if (request.IsActive) await ValidateReferencesAsync(ToRequest(plan), ct);
            var previous = plan.IsActive;
            plan.IsActive = request.IsActive;
            plan.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            audit.Record("maintenance_plan.status_changed", nameof(MaintenancePlan), id,
                new { IsActive = previous }, new { plan.IsActive });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return ToDto(plan);
    }

    public async Task<ChecklistTemplateDto> GetChecklistAsync(Guid id, int? version = null,
        CancellationToken ct = default)
    {
        if (version is <= 0) throw new AppException(400, "Checklist version must be positive.");
        var visiblePlans = VisiblePlans().AsNoTracking();
        var query = db.ChecklistTemplates.AsNoTracking().Include(x => x.Items)
            .Where(x => x.MaintenancePlanId == id && visiblePlans.Any(plan => plan.Id == x.MaintenancePlanId));
        if (version.HasValue) query = query.Where(x => x.Version == version);
        else query = query.Where(x => x.IsActive);
        var template = await query.OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct)
            ?? throw new AppException(404, "Maintenance plan or checklist version not found.");
        return ToDto(template);
    }

    public async Task<ChecklistTemplateDto> SetChecklistAsync(Guid id, ChecklistWriteRequest request,
        CancellationToken ct = default)
    {
        RequireManager();
        var normalized = ValidateChecklist(request);
        await using var transaction = await db.BeginTransactionAsync(ct);
        var plan = await FindAsync(id, ct);
        var latestVersion = await db.ChecklistTemplates.Where(x => x.MaintenancePlanId == id)
            .OrderByDescending(x => x.Version).Select(x => (int?)x.Version).FirstOrDefaultAsync(ct) ?? 0;
        if (latestVersion == int.MaxValue)
            throw new AppException(409, "No further checklist version numbers are available.");

        // Every save is a new version. Used and unused versions are both retained unchanged.
        var template = NewTemplate(id, latestVersion + 1, normalized);
        db.ChecklistTemplates.Add(template);
        plan.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        plan.Version = Guid.NewGuid();
        audit.Record("checklist.version_created", nameof(ChecklistTemplate), template.Id, newValues: ToDto(template));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(template);
    }

    private IQueryable<MaintenancePlan> VisiblePlans()
    {
        var userId = Guard.Authenticated(currentUser);
        if (currentUser.Roles.Contains(RoleNames.Manager) || currentUser.Roles.Contains(RoleNames.Admin))
            return db.MaintenancePlans;
        if (!currentUser.Roles.Contains(RoleNames.Supervisor))
            throw new AppException(403, "Maintenance planning access requires MANAGER, ADMIN, or SUPERVISOR access.");
        return db.MaintenancePlans.Where(plan => plan.SupervisorId == userId);
    }

    private void RequireManager() => Guard.RequireRole(currentUser, RoleNames.Manager, RoleNames.Admin);

    private async Task<MaintenancePlan> FindAsync(Guid id, CancellationToken ct) =>
        await db.MaintenancePlans.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new AppException(404, "Maintenance plan not found.");

    private PlanWriteRequest ValidatePlan(PlanWriteRequest request)
    {
        var name = Guard.Required(request.PlanName, "PlanName");
        if (!Enum.IsDefined(request.Priority)) throw new AppException(400, "Priority is not valid.");
        recurrence.Validate(request.FrequencyType, request.FrequencyValue);
        if (request.StartDate == default)
            throw new AppException(400, "StartDate must be a valid planning date.");
        // Reject schedules whose first interval cannot advance within the supported calendar.
        recurrence.Next(request.StartDate, request.StartDate, request.FrequencyType, request.FrequencyValue);
        if (request.EstimatedDurationMinutes is < 0)
            throw new AppException(400, "EstimatedDurationMinutes cannot be negative.");
        if (request.MinimumPhotoCount < 0)
            throw new AppException(400, "MinimumPhotoCount cannot be negative.");
        if ((request.PhotoRequired && request.MinimumPhotoCount == 0)
            || (!request.PhotoRequired && request.MinimumPhotoCount != 0))
            throw new AppException(400, "PhotoRequired requires a positive MinimumPhotoCount; otherwise the count must be zero.");
        return request with
        {
            PlanName = name,
            Instructions = PlanningValidation.Optional(request.Instructions, "Instructions", 10000)
        };
    }

    private async Task ValidateReferencesAsync(PlanWriteRequest request, CancellationToken ct)
    {
        if (!await db.Machines.AnyAsync(x => x.Id == request.MachineId && x.IsActive
            && x.Status != MachineStatus.Decommissioned, ct))
            throw new AppException(400, "Machine must exist and be active and not decommissioned.");
        if (!await db.MaintenanceTypes.AnyAsync(x => x.Id == request.MaintenanceTypeId && x.IsActive, ct))
            throw new AppException(400, "Maintenance type must exist and be active.");
        if (!await db.Users.AnyAsync(x => x.Id == request.SupervisorId && x.IsActive
            && x.UserRoles.Any(role => role.Role.Name == RoleNames.Supervisor), ct))
            throw new AppException(400, "Supervisor must be an active user with the SUPERVISOR role.");
        if (request.DefaultTechnicianId.HasValue && !await db.Users.AnyAsync(x => x.Id == request.DefaultTechnicianId
            && x.IsActive && x.UserRoles.Any(role => role.Role.Name == RoleNames.Technician), ct))
            throw new AppException(400, "Default technician must be an active user with the TECHNICIAN role.");
    }

    private static ChecklistWriteRequest ValidateChecklist(ChecklistWriteRequest request)
    {
        var name = Guard.Required(request.Name, "Name");
        if (request.Items is null || request.Items.Count is < 1 or > 200)
            throw new AppException(400, "A checklist must contain between 1 and 200 items.");
        var sequences = new HashSet<int>();
        var items = new List<ChecklistItemWriteRequest>(request.Items.Count);
        foreach (var item in request.Items)
        {
            if (item is null) throw new AppException(400, "Checklist items cannot be null.");
            if (item.SequenceNumber < 1 || !sequences.Add(item.SequenceNumber))
                throw new AppException(400, "Checklist sequence numbers must be positive and unique within the version.");
            if (!Enum.IsDefined(item.ResponseType)) throw new AppException(400, "Checklist response type is not valid.");
            var title = Guard.Required(item.Title, "Title", 500);
            var description = PlanningValidation.Optional(item.Description, "Description", 4000);
            var unit = PlanningValidation.Optional(item.Unit, "Unit", 50);
            if (item.ResponseType != ChecklistResponseType.NUMBER
                && (unit is not null || item.MinimumValue.HasValue || item.MaximumValue.HasValue))
                throw new AppException(400, "Only NUMBER checklist items may define a unit or numeric limits.");
            if (item.MinimumValue.HasValue && item.MaximumValue.HasValue && item.MinimumValue > item.MaximumValue)
                throw new AppException(400, "MinimumValue cannot exceed MaximumValue.");
            items.Add(item with { Title = title, Description = description, Unit = unit });
        }
        return new(name, items.OrderBy(x => x.SequenceNumber).ToArray());
    }

    private MaintenancePlan NewPlan(PlanWriteRequest request)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var plan = new MaintenancePlan
        {
            PlanName = request.PlanName, NextDueDate = request.StartDate,
            CreatedByUserId = currentUser.UserId!.Value, CreatedAt = now, UpdatedAt = now
        };
        ApplyPlanFields(plan, request);
        return plan;
    }

    private ChecklistTemplate NewTemplate(Guid planId, int version, ChecklistWriteRequest request)
    {
        var template = new ChecklistTemplate
        {
            MaintenancePlanId = planId, Version = version, Name = request.Name,
            CreatedByUserId = currentUser.UserId!.Value, CreatedAt = clock.GetUtcNow().UtcDateTime,
            IsActive = true
        };
        foreach (var item in request.Items)
            template.Items.Add(new ChecklistItem
            {
                ChecklistTemplateId = template.Id,
                SequenceNumber = item.SequenceNumber,
                Title = item.Title,
                Description = item.Description,
                ResponseType = item.ResponseType,
                IsMandatory = item.IsMandatory,
                Unit = item.Unit,
                MinimumValue = item.MinimumValue,
                MaximumValue = item.MaximumValue,
                PhotoRequired = item.PhotoRequired
            });
        return template;
    }

    private static void ApplyPlanFields(MaintenancePlan plan, PlanWriteRequest request)
    {
        plan.MachineId = request.MachineId;
        plan.PlanName = request.PlanName;
        plan.MaintenanceTypeId = request.MaintenanceTypeId;
        plan.Priority = request.Priority;
        plan.FrequencyType = request.FrequencyType;
        plan.FrequencyValue = request.FrequencyValue;
        plan.StartDate = request.StartDate;
        plan.DefaultTechnicianId = request.DefaultTechnicianId;
        plan.SupervisorId = request.SupervisorId;
        plan.Instructions = request.Instructions;
        plan.EstimatedDurationMinutes = request.EstimatedDurationMinutes;
        plan.PhotoRequired = request.PhotoRequired;
        plan.MinimumPhotoCount = request.MinimumPhotoCount;
        plan.CommentRequired = request.CommentRequired;
        plan.IsActive = request.IsActive;
    }

    private static PlanWriteRequest ToRequest(MaintenancePlan plan) => new(
        plan.MachineId, plan.PlanName, plan.MaintenanceTypeId, plan.Priority, plan.FrequencyType,
        plan.FrequencyValue, plan.StartDate, plan.SupervisorId, plan.DefaultTechnicianId, plan.Instructions,
        plan.EstimatedDurationMinutes, plan.PhotoRequired, plan.MinimumPhotoCount, plan.CommentRequired, plan.IsActive);

    private static MaintenancePlanDto ToDto(MaintenancePlan plan) => new(
        plan.Id, plan.MachineId, plan.PlanName, plan.MaintenanceTypeId, plan.Priority,
        plan.FrequencyType, plan.FrequencyValue, plan.StartDate, plan.NextDueDate,
        plan.DefaultTechnicianId, plan.SupervisorId, plan.Instructions, plan.EstimatedDurationMinutes,
        plan.PhotoRequired, plan.MinimumPhotoCount, plan.CommentRequired, plan.IsActive,
        plan.CreatedByUserId, plan.CreatedAt, plan.UpdatedAt);

    private static ChecklistTemplateDto ToDto(ChecklistTemplate template) => new(
        template.Id, template.MaintenancePlanId, template.Version, template.Name, template.IsActive,
        template.CreatedByUserId, template.CreatedAt,
        template.Items.OrderBy(x => x.SequenceNumber).Select(item => new ChecklistItemDto(
            item.Id, item.ChecklistTemplateId, item.SequenceNumber, item.Title, item.Description,
            item.ResponseType, item.IsMandatory, item.Unit, item.MinimumValue, item.MaximumValue,
            item.PhotoRequired)).ToArray());
}
