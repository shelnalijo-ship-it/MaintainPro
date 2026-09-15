using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.Notifications;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Breakdowns;

public sealed class BreakdownService(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock, IGenerationConcurrency concurrency)
{
    public async Task<PagedResult<BreakdownDto>> ListAsync(BreakdownQuery request, CancellationToken ct = default)
    {
        Guard.Page(request.Page, request.PageSize);
        if (request.Severity.HasValue && !Enum.IsDefined(request.Severity.Value))
            throw new AppException(400, "Severity is invalid.");
        if (request.Status.HasValue && !Enum.IsDefined(request.Status.Value))
            throw new AppException(400, "Status is invalid.");
        if (request.ReportedFrom.HasValue && request.ReportedTo.HasValue && request.ReportedFrom > request.ReportedTo)
            throw new AppException(400, "ReportedFrom cannot follow ReportedTo.");
        var query = BreakdownAccess.Visible(db, currentUser).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = Guard.Required(request.Search, "Search", 200).ToUpperInvariant();
            query = query.Where(x => x.BreakdownNumber.ToUpper().Contains(search) ||
                x.MachineCode.ToUpper().Contains(search) || x.MachineName.ToUpper().Contains(search) ||
                x.Description.ToUpper().Contains(search));
        }
        if (request.MachineId.HasValue) query = query.Where(x => x.MachineId == request.MachineId);
        if (request.TechnicianId.HasValue) query = query.Where(x => x.AssignedTechnicianId == request.TechnicianId);
        if (request.SupervisorId.HasValue) query = query.Where(x => x.SupervisorId == request.SupervisorId);
        if (request.Severity.HasValue) query = query.Where(x => x.Severity == request.Severity);
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status);
        if (request.MachineStopped.HasValue) query = query.Where(x => x.MachineStopped == request.MachineStopped);
        if (request.OpenOnly == true) query = query.Where(x => x.Status != BreakdownStatus.CLOSED && x.Status != BreakdownStatus.CANCELLED);
        if (request.ReportedFrom.HasValue)
        {
            var from = request.ReportedFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(x => x.ReportedAt >= from);
        }
        if (request.ReportedTo.HasValue)
        {
            var to = request.ReportedTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(x => x.ReportedAt <= to);
        }
        var total = await query.CountAsync(ct);
        var entries = await query.OrderByDescending(x => x.ReportedAt).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        return new(entries.Select(x => ToDto(x, now)).ToArray(), request.Page, request.PageSize, total);
    }

    public async Task<BreakdownDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var breakdown = await BreakdownAccess.Visible(db, currentUser).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "Breakdown not found.");
        return ToDto(breakdown, clock.GetUtcNow().UtcDateTime);
    }

    public async Task<PagedResult<BreakdownDto>> MachineHistoryAsync(Guid machineId, BreakdownQuery query,
        CancellationToken ct = default)
    {
        if (!await ReportingMachines().AnyAsync(x => x.Id == machineId, ct) &&
            !await BreakdownAccess.Visible(db, currentUser).AnyAsync(x => x.MachineId == machineId, ct))
            throw new AppException(404, "Machine not found.");
        return await ListAsync(query with { MachineId = machineId }, ct);
    }

    public async Task<IReadOnlyList<BreakdownHistoryDto>> HistoryAsync(Guid id, CancellationToken ct = default)
    {
        var visible = BreakdownAccess.Visible(db, currentUser);
        if (!await visible.AnyAsync(x => x.Id == id, ct)) throw new AppException(404, "Breakdown not found.");
        return await db.BreakdownHistoryEvents.AsNoTracking()
            .Where(x => x.BreakdownId == id && visible.Any(b => b.Id == x.BreakdownId))
            .OrderBy(x => x.SequenceNumber).Select(x => new BreakdownHistoryDto(x.Id, x.SequenceNumber,
                x.Action, x.ActorUserId, x.ActorName, x.OccurredAt, x.CorrectiveSubmissionId, x.SubmissionVersion, x.Details))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BreakdownAssignmentDto>> AssignmentsAsync(Guid id, CancellationToken ct = default)
    {
        var visible = BreakdownAccess.Visible(db, currentUser);
        if (!await visible.AnyAsync(x => x.Id == id, ct)) throw new AppException(404, "Breakdown not found.");
        return await db.BreakdownAssignmentHistories.AsNoTracking()
            .Where(x => x.BreakdownId == id && visible.Any(b => b.Id == x.BreakdownId))
            .OrderBy(x => x.SequenceNumber).Select(x => new BreakdownAssignmentDto(x.Id, x.SequenceNumber,
                x.TechnicianId, x.TechnicianEmployeeId, x.TechnicianName, x.SupervisorId,
                x.SupervisorEmployeeId, x.SupervisorName, x.AssignedByUserId, x.AssignedAt, x.Reason))
            .ToListAsync(ct);
    }

    public Task<BreakdownDto> ReportAsync(BreakdownReportRequest request, CancellationToken ct = default)
    {
        Guard.RequireRole(currentUser, RoleNames.Technician, RoleNames.Supervisor, RoleNames.Manager, RoleNames.Admin);
        if (!Enum.IsDefined(request.Severity)) throw new AppException(400, "Severity is invalid.");
        var description = Guard.Required(request.Description, "Description", 10000);
        var observation = Optional(request.InitialObservation, "InitialObservation", 10000);
        return WriteAsync(async () =>
        {
            await using var transaction = await db.BeginTransactionAsync(ct);
            var machine = await ReportingMachines().SingleOrDefaultAsync(x => x.Id == request.MachineId, ct)
                ?? throw new AppException(404, "Machine not found.");
            if (request.SupervisorId.HasValue && request.SupervisorId != machine.SupervisorUserId && !BreakdownAccess.CanManage(currentUser))
                throw new AppException(403, "Only manager/admin can override the machine's reporting supervisor.");
            var supervisorId = request.SupervisorId ?? machine.SupervisorUserId
                ?? throw new AppException(400, "An active supervisor must be selected for this breakdown.");
            var supervisor = await ActiveRoleAsync(supervisorId, RoleNames.Supervisor, ct);
            var reporter = await db.Users.SingleOrDefaultAsync(x => x.Id == currentUser.UserId && x.IsActive, ct)
                ?? throw new AppException(403, "The reporting user must be active.");
            var now = clock.GetUtcNow().UtcDateTime;
            var breakdown = new Breakdown
            {
                BreakdownNumber = await new BreakdownNumberAllocator(db).AllocateAsync(now, ct),
                MachineId = machine.Id, MachineCode = machine.MachineCode, MachineName = machine.Name,
                ReportedByUserId = reporter.Id, ReporterEmployeeId = reporter.EmployeeId, ReporterName = Name(reporter),
                ReportedAt = now, Severity = request.Severity, MachineStopped = request.MachineStopped,
                Description = description, InitialObservation = observation,
                SupervisorId = supervisor.Id, SupervisorEmployeeId = supervisor.EmployeeId, SupervisorName = Name(supervisor),
                CreatedAt = now, UpdatedAt = now
            };
            if (breakdown.MachineStopped)
            {
                var previous = machine.Status;
                breakdown.PreviousMachineStatus = previous;
                if (previous is MachineStatus.Operational or MachineStatus.Standby)
                {
                    machine.Status = MachineStatus.Breakdown;
                    machine.StatusVersion = Guid.NewGuid();
                    breakdown.RestoreMachineStatus = previous;
                }
                else if (previous == MachineStatus.Breakdown)
                {
                    breakdown.RestoreMachineStatus = await db.Breakdowns.AsNoTracking()
                        .Where(x => x.MachineId == machine.Id && x.MachineStopped && x.ReturnedToServiceAt == null &&
                            x.MachineStatusVersionAtStop == machine.StatusVersion)
                        .OrderBy(x => x.ReportedAt).ThenBy(x => x.Id)
                        .Select(x => x.RestoreMachineStatus).FirstOrDefaultAsync(ct);
                }
                breakdown.MachineStatusVersionAtStop = machine.StatusVersion;
                // Even a second report against an already stopped machine participates in the
                // machine write conflict, serializing it with another report or a return action.
                machine.Version = Guid.NewGuid();
                if (previous != machine.Status)
                    audit.Record("Machine.StatusChanged", nameof(Machine), machine.Id,
                        new { Status = previous }, new { machine.Status, BreakdownId = breakdown.Id });
            }
            db.Breakdowns.Add(breakdown);
            BreakdownHistory.Record(db, currentUser, breakdown, "Breakdown.Reported", now,
                details: new { breakdown.Severity, breakdown.MachineStopped, breakdown.PreviousMachineStatus });
            audit.Record("Breakdown.Reported", nameof(Breakdown), breakdown.Id, newValues: ToDto(breakdown, now));
            var notifications = new BreakdownNotificationService(db, currentUser, audit, clock);
            await notifications.EnsureAsync(breakdown, NotificationType.BREAKDOWN_REPORTED, ct: ct);
            if (breakdown.Severity == BreakdownSeverity.CRITICAL)
            {
                BreakdownHistory.Record(db, currentUser, breakdown, "Breakdown.CriticalEscalation", now);
                audit.Record("Breakdown.CriticalEscalation", nameof(Breakdown), breakdown.Id,
                    newValues: new { breakdown.SupervisorId, ManagerStrategy = "All active MANAGER users" });
                await notifications.EnsureAsync(breakdown, NotificationType.BREAKDOWN_CRITICAL, ct: ct);
            }
            await db.SaveChangesAsync(ct);
            var result = ToDto(breakdown, now);
            await transaction.CommitAsync(ct);
            return result;
        }, ct);
    }

    public Task<BreakdownDto> AssignAsync(Guid id, BreakdownAssignmentRequest request, CancellationToken ct = default)
    {
        var reason = Optional(request.Reason, "Reason", 2000);
        return WriteAsync(async () =>
        {
            await using var transaction = await db.BeginTransactionAsync(ct);
            var breakdown = await BreakdownAccess.LoadAsync(db, currentUser, id, ct);
            BreakdownAccess.RequireAssignmentManager(currentUser, breakdown);
            if (breakdown.Status is not (BreakdownStatus.REPORTED or BreakdownStatus.ASSIGNED or BreakdownStatus.REJECTED))
                throw new AppException(409, "Reassignment is allowed before execution or after rejection.");
            var supervisorId = request.SupervisorId ?? breakdown.SupervisorId;
            if (supervisorId != breakdown.SupervisorId && !BreakdownAccess.CanManage(currentUser))
                throw new AppException(403, "Only manager/admin can change the assigned supervisor.");
            var technician = await ActiveRoleAsync(request.TechnicianId, RoleNames.Technician, ct);
            var supervisor = await ActiveRoleAsync(supervisorId, RoleNames.Supervisor, ct);
            var now = clock.GetUtcNow().UtcDateTime;
            if (breakdown.AssignedTechnicianId == technician.Id && breakdown.SupervisorId == supervisor.Id)
            {
                await transaction.CommitAsync(ct);
                return ToDto(breakdown, now);
            }
            var previous = new { breakdown.AssignedTechnicianId, breakdown.SupervisorId };
            var reassigned = breakdown.AssignedTechnicianId.HasValue;
            breakdown.AssignedTechnicianId = technician.Id;
            breakdown.TechnicianEmployeeId = technician.EmployeeId;
            breakdown.TechnicianName = Name(technician);
            breakdown.SupervisorId = supervisor.Id;
            breakdown.SupervisorEmployeeId = supervisor.EmployeeId;
            breakdown.SupervisorName = Name(supervisor);
            breakdown.AssignmentVersion = checked(breakdown.AssignmentVersion + 1);
            if (breakdown.Status == BreakdownStatus.REPORTED) breakdown.Status = BreakdownStatus.ASSIGNED;
            breakdown.UpdatedAt = now;
            db.BreakdownAssignmentHistories.Add(new BreakdownAssignmentHistory
            {
                BreakdownId = id, SequenceNumber = breakdown.AssignmentVersion,
                TechnicianId = technician.Id, TechnicianEmployeeId = technician.EmployeeId, TechnicianName = Name(technician),
                SupervisorId = supervisor.Id, SupervisorEmployeeId = supervisor.EmployeeId, SupervisorName = Name(supervisor),
                AssignedByUserId = currentUser.UserId!.Value, AssignedAt = now, Reason = reason
            });
            var action = reassigned ? "Breakdown.Reassigned" : "Breakdown.Assigned";
            BreakdownHistory.Record(db, currentUser, breakdown, action, now,
                details: new { previous, breakdown.AssignedTechnicianId, breakdown.SupervisorId, Reason = reason });
            audit.Record(action, nameof(Breakdown), id, previous,
                new { breakdown.AssignedTechnicianId, breakdown.SupervisorId, Reason = reason });
            await new BreakdownNotificationService(db, currentUser, audit, clock)
                .EnsureAsync(breakdown, NotificationType.BREAKDOWN_ASSIGNED, ct: ct);
            await db.SaveChangesAsync(ct);
            var result = ToDto(breakdown, now);
            await transaction.CommitAsync(ct);
            return result;
        }, ct);
    }

    public Task<BreakdownReturnDto> ReturnToServiceAsync(Guid id, BreakdownReturnRequest request, CancellationToken ct = default)
    {
        var remarks = Optional(request.Remarks, "Remarks", 2000);
        return WriteAsync(async () =>
        {
            await using var transaction = await db.BeginTransactionAsync(ct);
            var breakdown = await BreakdownAccess.LoadAsync(db, currentUser, id, ct);
            BreakdownAccess.RequireAssignmentManager(currentUser, breakdown);
            var machine = await db.Machines.SingleAsync(x => x.Id == breakdown.MachineId, ct);
            if (!breakdown.MachineStopped) throw new AppException(409, "This breakdown did not stop the machine.");
            if (breakdown.ReturnedToServiceAt is DateTime returnedAt)
            {
                await transaction.CommitAsync(ct);
                return new BreakdownReturnDto(id, machine.Id, machine.Status, returnedAt, new[] { id });
            }
            if (breakdown.Status != BreakdownStatus.CLOSED || !await HasApprovedLatestAsync(breakdown, ct))
                throw new AppException(409, "The latest corrective submission must be approved and the breakdown closed.");
            var stopped = await db.Breakdowns.Where(x => x.MachineId == machine.Id && x.MachineStopped &&
                x.ReturnedToServiceAt == null).OrderBy(x => x.ReportedAt).ThenBy(x => x.Id).ToListAsync(ct);
            if (stopped.Any(x => x.Status != BreakdownStatus.CLOSED))
                throw new AppException(409, "Another unresolved stopped-machine breakdown blocks return to service.");
            if (!machine.IsActive || machine.Status != MachineStatus.Breakdown)
                throw new AppException(409, "The machine is inactive, decommissioned, or has an independent maintenance/out-of-service state.");
            if (breakdown.MachineStatusVersionAtStop != machine.StatusVersion ||
                stopped.Any(x => x.MachineStatusVersionAtStop != machine.StatusVersion))
                throw new AppException(409, "The machine's status changed independently after the recorded stop. Return requires status reconciliation.");
            var restore = breakdown.RestoreMachineStatus;
            if (restore is not (MachineStatus.Operational or MachineStatus.Standby) ||
                stopped.Any(x => x.RestoreMachineStatus != restore))
                throw new AppException(409, "There is no consistent safe pre-stop operational state to restore.");
            foreach (var related in stopped)
                if (!await HasApprovedLatestAsync(related, ct))
                    throw new AppException(409, "Every stopped breakdown must have its latest corrective submission approved.");
            var now = clock.GetUtcNow().UtcDateTime;
            var previous = machine.Status;
            machine.Status = restore.Value;
            machine.StatusVersion = Guid.NewGuid();
            foreach (var related in stopped)
            {
                related.ReturnedToServiceAt = now;
                related.UpdatedAt = now;
                BreakdownHistory.Record(db, currentUser, related, "Machine.ReturnedToService", now,
                    details: new { PreviousStatus = previous, RestoredStatus = machine.Status, Remarks = remarks });
                audit.Record("Breakdown.ReturnedToService", nameof(Breakdown), related.Id,
                    newValues: new { ReturnedToServiceAt = now, DowntimeMinutes = BreakdownTiming.DowntimeMinutes(related, now), Remarks = remarks });
            }
            audit.Record("Machine.StatusChanged", nameof(Machine), machine.Id, new { Status = previous },
                new { machine.Status, ReturnedToServiceAt = now, BreakdownIds = stopped.Select(x => x.Id).ToArray(), Remarks = remarks });
            await db.SaveChangesAsync(ct);
            var ids = stopped.Select(x => x.Id).ToArray();
            // The physical return ends all approved stops in the episode, but the response still
            // respects the caller's record visibility when listing those associated breakdowns.
            var visibleIds = await BreakdownAccess.Visible(db, currentUser).Where(x => ids.Contains(x.Id))
                .OrderBy(x => x.ReportedAt).ThenBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
            await transaction.CommitAsync(ct);
            return new BreakdownReturnDto(id, machine.Id, machine.Status, now, visibleIds);
        }, ct);
    }

    private Task<bool> HasApprovedLatestAsync(Breakdown breakdown, CancellationToken ct) =>
        db.CorrectiveSubmissions.AnyAsync(x => x.BreakdownId == breakdown.Id &&
            x.VersionNumber == breakdown.SubmissionVersion && x.Review != null &&
            x.Review.Decision == CorrectiveReviewDecision.APPROVED, ct);

    private IQueryable<Machine> ReportingMachines()
    {
        var actor = Guard.Authenticated(currentUser);
        if (BreakdownAccess.CanManage(currentUser)) return db.Machines;
        var technician = currentUser.Roles.Contains(RoleNames.Technician);
        var supervisor = currentUser.Roles.Contains(RoleNames.Supervisor);
        return db.Machines.Where(x => (technician && x.MachineOwnerUserId == actor) || (supervisor && x.SupervisorUserId == actor));
    }

    private async Task<User> ActiveRoleAsync(Guid id, string role, CancellationToken ct) =>
        await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.IsActive && x.UserRoles.Any(r => r.Role.Name == role), ct)
        ?? throw new AppException(400, $"The selected user must be active and hold the {role} role.");

    private async Task<T> WriteAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (db.HasActiveTransaction) throw new InvalidOperationException("Breakdown commands must own their transactions.");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            concurrency.ResetTracking();
            try { return await action(); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception) when (concurrency.IsRetryable(exception))
            {
                if (attempt == 4) throw new AppException(409, "Concurrent breakdown changes prevented completion. Retry the command.");
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), ct);
            }
            finally { concurrency.ResetTracking(); }
        }
        throw new InvalidOperationException("The bounded breakdown retry loop exited unexpectedly.");
    }

    public static BreakdownDto ToDto(Breakdown x, DateTime now) => new(x.Id, x.BreakdownNumber,
        x.MachineId, x.MachineCode, x.MachineName, x.ReportedByUserId, x.ReporterName, x.ReportedAt, x.Severity,
        x.MachineStopped, x.Description, x.InitialObservation, x.AssignedTechnicianId, x.TechnicianName,
        x.SupervisorId, x.SupervisorName, x.Status, x.PreviousMachineStatus, x.StartedAt, x.CompletedAt,
        x.SubmittedAt, x.ClosedAt, x.ReturnedToServiceAt, BreakdownTiming.DowntimeMinutes(x, now),
        x.MachineStopped && !x.ReturnedToServiceAt.HasValue, x.CreatedAt, x.UpdatedAt, x.SubmissionVersion);

    private static string Name(User user) => user.FirstName + " " + user.LastName;
    private static string? Optional(string? value, string name, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : Guard.Required(value, name, max);
}
