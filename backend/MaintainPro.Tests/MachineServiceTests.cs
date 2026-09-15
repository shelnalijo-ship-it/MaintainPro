using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Machines;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using MaintainPro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class MachineServiceTests
{
    [Fact]
    public async Task Valid_machine_creation_stores_all_fields_assignments_and_audit()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var administrator = await fixture.AsAdminAsync();
        var owner = await fixture.SeedUserAsync("TECHNICIAN");
        var supervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var department = new Department { Name = "Production" };
        var category = new MachineCategory { Name = "Pumps" };
        var location = new Location { Name = "Pump room", DepartmentId = department.Id };
        fixture.Db.AddRange(department, category, location);
        await fixture.Db.SaveChangesAsync();
        var request = new MachineWriteRequest
        {
            MachineCode = " pump-001 ", AssetNumber = "ASSET-10", Name = "Cooling pump",
            CategoryId = category.Id, Manufacturer = "Test manufacturer", Model = "M-20", SerialNumber = "SN-99",
            DepartmentId = department.Id, LocationId = location.Id, InstallationDate = new DateOnly(2025, 1, 1),
            CommissioningDate = new DateOnly(2025, 1, 2), WarrantyExpiryDate = new DateOnly(2028, 1, 1),
            Status = MachineStatus.Standby, Criticality = MachineCriticality.High,
            CalibrationRequired = true, PreventiveMaintenanceRequired = true,
            MachineOwnerUserId = owner.Id, SupervisorUserId = supervisor.Id, Notes = "Commissioned safely",
            AssignmentReason = "Initial team"
        };

        var dto = await fixture.Machines.CreateAsync(request);

        Assert.Equal("PUMP-001", dto.MachineCode);
        Assert.Equal(request.AssetNumber, dto.AssetNumber);
        Assert.Equal(request.Name, dto.Name);
        Assert.Equal(request.CategoryId, dto.CategoryId);
        Assert.Equal(request.Manufacturer, dto.Manufacturer);
        Assert.Equal(request.Model, dto.Model);
        Assert.Equal(request.SerialNumber, dto.SerialNumber);
        Assert.Equal(request.DepartmentId, dto.DepartmentId);
        Assert.Equal(request.LocationId, dto.LocationId);
        Assert.Equal(request.InstallationDate, dto.InstallationDate);
        Assert.Equal(request.CommissioningDate, dto.CommissioningDate);
        Assert.Equal(request.WarrantyExpiryDate, dto.WarrantyExpiryDate);
        Assert.Equal(request.Status, dto.Status);
        Assert.Equal(request.Criticality, dto.Criticality);
        Assert.True(dto.CalibrationRequired);
        Assert.True(dto.PreventiveMaintenanceRequired);
        Assert.Equal(owner.Id, dto.MachineOwnerUserId);
        Assert.Equal(supervisor.Id, dto.SupervisorUserId);
        Assert.Equal(request.Notes, dto.Notes);
        var history = Assert.Single(await fixture.Db.MachineAssignmentHistories.ToListAsync());
        Assert.Equal(owner.Id, history.TechnicianId);
        Assert.Equal(supervisor.Id, history.SupervisorId);
        Assert.Equal(administrator.Id, history.AssignedByUserId);
        Assert.Null(history.EffectiveTo);
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), log => log.Action == "Machine.Created"
            && log.EntityId == dto.Id.ToString());
    }

    [Fact]
    public async Task Duplicate_machine_code_is_rejected_without_a_partial_machine_or_audit()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        await fixture.Machines.CreateAsync(NewMachine("PUMP-001"));
        var audits = await fixture.Db.AuditLogs.CountAsync();

        await ModuleFixture.ExpectStatusAsync(409,
            () => fixture.Machines.CreateAsync(NewMachine(" pump-001 ")));

        Assert.Equal(1, await fixture.Db.Machines.CountAsync());
        Assert.Equal(audits, await fixture.Db.AuditLogs.CountAsync());
    }

    [Theory]
    [InlineData("SUPERVISOR", true)]
    [InlineData("ADMIN", true)]
    [InlineData("TECHNICIAN", false)]
    public async Task Owner_must_be_an_active_technician(string role, bool active)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var invalidOwner = await fixture.SeedUserAsync(role);
        invalidOwner.IsActive = active;
        await fixture.Db.SaveChangesAsync();

        await ModuleFixture.ExpectStatusAsync(400,
            () => fixture.Machines.CreateAsync(NewMachine() with { MachineOwnerUserId = invalidOwner.Id }));

        Assert.Empty(await fixture.Db.Machines.ToListAsync());
    }

    [Theory]
    [InlineData("TECHNICIAN", true)]
    [InlineData("MANAGER", true)]
    [InlineData("ADMIN", true)]
    [InlineData("SUPERVISOR", false)]
    public async Task Supervisor_must_hold_an_active_explicit_supervisor_role(string role, bool active)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var invalidSupervisor = await fixture.SeedUserAsync(role);
        invalidSupervisor.IsActive = active;
        await fixture.Db.SaveChangesAsync();

        await ModuleFixture.ExpectStatusAsync(400,
            () => fixture.Machines.CreateAsync(NewMachine() with { SupervisorUserId = invalidSupervisor.Id }));
    }

    [Fact]
    public async Task Administrator_with_explicit_supervisor_role_can_be_selected_as_supervisor()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var supervisor = await fixture.SeedUserAsync("ADMIN", "SUPERVISOR");

        var result = await fixture.Machines.CreateAsync(NewMachine() with { SupervisorUserId = supervisor.Id });

        Assert.Equal(supervisor.Id, result.SupervisorUserId);
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), log => log.Action == "Machine.SupervisorChanged");
    }

    [Theory]
    [InlineData("category")]
    [InlineData("department")]
    [InlineData("location")]
    [InlineData("owner")]
    [InlineData("supervisor")]
    public async Task Nonexistent_references_are_rejected_before_persistence(string reference)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var missing = Guid.NewGuid();
        var request = reference switch
        {
            "category" => NewMachine() with { CategoryId = missing },
            "department" => NewMachine() with { DepartmentId = missing },
            "location" => NewMachine() with { LocationId = missing },
            "owner" => NewMachine() with { MachineOwnerUserId = missing },
            _ => NewMachine() with { SupervisorUserId = missing }
        };

        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Machines.CreateAsync(request));

        Assert.Empty(await fixture.Db.Machines.ToListAsync());
        Assert.Empty(await fixture.Db.MachineAssignmentHistories.ToListAsync());
    }

    [Fact]
    public async Task Location_must_belong_to_selected_department()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var selectedDepartment = new Department { Name = "Selected" };
        var otherDepartment = new Department { Name = "Other" };
        var location = new Location { Name = "Other workshop", DepartmentId = otherDepartment.Id };
        fixture.Db.AddRange(selectedDepartment, otherDepartment, location);
        await fixture.Db.SaveChangesAsync();

        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Machines.CreateAsync(NewMachine() with
        {
            DepartmentId = selectedDepartment.Id, LocationId = location.Id
        }));
    }

    [Fact]
    public async Task Owner_and_supervisor_changes_close_previous_history_and_preserve_original_values()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var administrator = await fixture.AsAdminAsync();
        var firstOwner = await fixture.SeedUserAsync("TECHNICIAN");
        var nextOwner = await fixture.SeedUserAsync("TECHNICIAN");
        var firstSupervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var nextSupervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var machine = await fixture.Machines.CreateAsync(NewMachine() with
        {
            MachineOwnerUserId = firstOwner.Id, SupervisorUserId = firstSupervisor.Id,
            AssignmentReason = "Initial assignment"
        });
        var original = Assert.Single(await fixture.Db.MachineAssignmentHistories.AsNoTracking().ToListAsync());

        await fixture.Machines.AssignOwnerAsync(machine.Id,
            new MachineAssignmentRequest(nextOwner.Id, firstSupervisor.Id, "Shift handover"));
        await fixture.Machines.AssignOwnerAsync(machine.Id,
            new MachineAssignmentRequest(nextOwner.Id, nextSupervisor.Id, "Supervisor handover"));
        var history = await fixture.Machines.GetAssignmentHistoryAsync(machine.Id);

        Assert.Equal(3, history.Count);
        var preserved = history.Single(row => row.Id == original.Id);
        Assert.Equal(firstOwner.Id, preserved.TechnicianId);
        Assert.Equal(firstSupervisor.Id, preserved.SupervisorId);
        Assert.Equal(original.EffectiveFrom, preserved.EffectiveFrom);
        Assert.Equal("Initial assignment", preserved.Reason);
        Assert.NotNull(preserved.EffectiveTo);
        var active = Assert.Single(history, row => !row.EffectiveTo.HasValue);
        Assert.Equal(nextOwner.Id, active.TechnicianId);
        Assert.Equal(nextSupervisor.Id, active.SupervisorId);
        Assert.Equal(administrator.Id, active.AssignedByUserId);
        Assert.Equal("Supervisor handover", active.Reason);
        var audits = await fixture.Db.AuditLogs.ToListAsync();
        Assert.Contains(audits, log => log.Action == "Machine.OwnerChanged"
            && log.NewValuesJson!.Contains("Shift handover"));
        Assert.Contains(audits, log => log.Action == "Machine.SupervisorChanged"
            && log.NewValuesJson!.Contains("Supervisor handover"));

        await fixture.Machines.AssignOwnerAsync(machine.Id,
            new MachineAssignmentRequest(nextOwner.Id, nextSupervisor.Id, "No actual change"));
        Assert.Equal(3, await fixture.Db.MachineAssignmentHistories.CountAsync());
    }

    [Fact]
    public async Task Clearing_owner_closes_history_and_supervisor_only_changes_remain_audited()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var technician = await fixture.SeedUserAsync("TECHNICIAN");
        var supervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var machine = await fixture.Machines.CreateAsync(NewMachine() with { MachineOwnerUserId = technician.Id });

        await fixture.Machines.AssignOwnerAsync(machine.Id, new MachineAssignmentRequest(null, supervisor.Id, "Unassigned"));

        Assert.Null((await fixture.Machines.GetAsync(machine.Id)).MachineOwnerUserId);
        Assert.NotNull(Assert.Single(await fixture.Db.MachineAssignmentHistories.ToListAsync()).EffectiveTo);
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), log => log.Action == "Machine.SupervisorChanged");
    }

    [Fact]
    public async Task Assignment_transaction_rolls_back_history_closure_when_audit_cannot_be_written()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var firstOwner = await fixture.SeedUserAsync("TECHNICIAN");
        var nextOwner = await fixture.SeedUserAsync("TECHNICIAN");
        var machine = await fixture.Machines.CreateAsync(NewMachine() with { MachineOwnerUserId = firstOwner.Id });
        var auditCount = await fixture.Db.AuditLogs.CountAsync();
        var service = new MachineService(fixture.Db, fixture.Actor, new RejectingAuditWriter());

        await Assert.ThrowsAsync<AuditUnavailableException>(() => service.AssignOwnerAsync(machine.Id,
            new MachineAssignmentRequest(nextOwner.Id, null, "Must roll back")));
        fixture.Db.ChangeTracker.Clear();

        Assert.Equal(firstOwner.Id, (await fixture.Db.Machines.SingleAsync()).MachineOwnerUserId);
        Assert.Null(Assert.Single(await fixture.Db.MachineAssignmentHistories.ToListAsync()).EffectiveTo);
        Assert.Equal(auditCount, await fixture.Db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Editing_machine_and_decommissioning_preserves_record_and_audits_status()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var machine = await fixture.Machines.CreateAsync(NewMachine());

        var edited = await fixture.Machines.UpdateAsync(machine.Id, NewMachine() with
        {
            Name = "Updated pump", Notes = "Replaced bearings", Criticality = MachineCriticality.Critical
        });
        var stopped = await fixture.Machines.SetStatusAsync(machine.Id,
            new MachineStatusRequest(MachineStatus.Decommissioned, true));

        Assert.Equal("Updated pump", edited.Name);
        Assert.Equal(MachineCriticality.Critical, edited.Criticality);
        Assert.Equal(MachineStatus.Decommissioned, stopped.Status);
        Assert.False(stopped.IsActive);
        Assert.Equal(1, await fixture.Db.Machines.CountAsync());
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), log => log.Action == "Machine.Updated");
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), log => log.Action == "Machine.StatusChanged");
        fixture.Db.Machines.Remove(await fixture.Db.Machines.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Concurrent_machine_edits_cannot_silently_overwrite_the_first_change()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var machine = await fixture.SeedMachineAsync();
        await using var staleContext = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(fixture.Db.Database.GetDbConnection()).Options);
        var stale = await staleContext.Machines.SingleAsync();
        machine.Name = "First editor's change";
        await fixture.Db.SaveChangesAsync();
        stale.Name = "Second editor's stale change";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleContext.SaveChangesAsync());

        Assert.Equal("First editor's change", (await fixture.Db.Machines.AsNoTracking().SingleAsync()).Name);
    }

    [Theory]
    [InlineData("TECHNICIAN", 1)]
    [InlineData("SUPERVISOR", 1)]
    [InlineData("MANAGER", 3)]
    [InlineData("ADMIN", 3)]
    [InlineData("TECHNICIAN|SUPERVISOR", 2)]
    [InlineData("", 0)]
    public async Task Role_visibility_is_the_union_of_current_assignments_before_pagination(string roles, int expected)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var viewer = await fixture.SeedUserAsync(roles.Split('|', StringSplitOptions.RemoveEmptyEntries));
        var otherUser = await fixture.SeedUserAsync("TECHNICIAN", "SUPERVISOR");
        // A referenced user may have changed role since assignment; eligibility for visibility is current.
        var owned = await fixture.SeedMachineAsync(viewer.Id, otherUser.Id);
        var supervised = await fixture.SeedMachineAsync(otherUser.Id, viewer.Id);
        var hidden = await fixture.SeedMachineAsync(otherUser.Id, otherUser.Id);
        fixture.ActAs(viewer);

        var firstPage = await fixture.Machines.ListAsync(new MachineQuery { Page = 1, PageSize = 1 });
        var full = await fixture.Machines.ListAsync(new MachineQuery());

        Assert.Equal(expected, firstPage.TotalCount);
        Assert.Equal(Math.Min(1, expected), firstPage.Items.Count);
        Assert.Equal(expected, full.Items.Count);
        if (roles.Contains("TECHNICIAN")) Assert.Contains(full.Items, machine => machine.Id == owned.Id);
        if (roles.Contains("SUPERVISOR")) Assert.Contains(full.Items, machine => machine.Id == supervised.Id);
        if (roles is "MANAGER" or "ADMIN")
            Assert.Equal(hidden.Id, (await fixture.Machines.GetAsync(hidden.Id)).Id);
        else
        {
            Assert.DoesNotContain(full.Items, machine => machine.Id == hidden.Id);
            await ModuleFixture.ExpectStatusAsync(404, () => fixture.Machines.GetAsync(hidden.Id));
            await ModuleFixture.ExpectStatusAsync(404, () => fixture.Machines.GetAssignmentHistoryAsync(hidden.Id));
        }
    }

    [Fact]
    public async Task Every_machine_filter_can_be_combined_and_paging_is_stable()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var owner = await fixture.SeedUserAsync("TECHNICIAN");
        var supervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var department = new Department { Name = "Quality" };
        var category = new MachineCategory { Name = "Sensors" };
        var location = new Location { Name = "Lab", DepartmentId = department.Id };
        fixture.Db.AddRange(department, category, location);
        await fixture.Db.SaveChangesAsync();
        var match = await fixture.SeedMachineAsync(owner.Id, supervisor.Id, machine =>
        {
            machine.CategoryId = category.Id; machine.DepartmentId = department.Id; machine.LocationId = location.Id;
            machine.Status = MachineStatus.Standby; machine.Criticality = MachineCriticality.High;
            machine.CalibrationRequired = true; machine.Name = "Precision gauge";
        });
        await fixture.SeedMachineAsync(configure: machine => machine.IsActive = false);
        await fixture.SeedMachineAsync(configure: machine => machine.IsActive = false);

        var individualFilters = new[]
        {
            new MachineQuery { CategoryId = category.Id },
            new MachineQuery { DepartmentId = department.Id },
            new MachineQuery { LocationId = location.Id },
            new MachineQuery { Status = MachineStatus.Standby },
            new MachineQuery { Criticality = MachineCriticality.High },
            new MachineQuery { CalibrationRequired = true },
            new MachineQuery { OwnerUserId = owner.Id },
            new MachineQuery { SupervisorUserId = supervisor.Id },
            new MachineQuery { IsActive = true }
        };
        foreach (var filter in individualFilters)
            Assert.Equal(match.Id, Assert.Single((await fixture.Machines.ListAsync(filter)).Items).Id);
        Assert.Equal(2, (await fixture.Machines.ListAsync(new MachineQuery { IsActive = false })).TotalCount);
        Assert.Equal(2, (await fixture.Machines.ListAsync(new MachineQuery { CalibrationRequired = false })).TotalCount);

        var result = await fixture.Machines.ListAsync(new MachineQuery
        {
            Search = "precision", CategoryId = category.Id, DepartmentId = department.Id, LocationId = location.Id,
            Status = MachineStatus.Standby, Criticality = MachineCriticality.High, CalibrationRequired = true,
            OwnerUserId = owner.Id, SupervisorUserId = supervisor.Id, IsActive = true, Page = 1, PageSize = 1
        });
        var pageOne = await fixture.Machines.ListAsync(new MachineQuery { Page = 1, PageSize = 2 });
        var pageTwo = await fixture.Machines.ListAsync(new MachineQuery { Page = 2, PageSize = 2 });

        Assert.Equal(match.Id, Assert.Single(result.Items).Id);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(3, pageTwo.TotalCount);
        Assert.Equal(2, pageTwo.Page);
        Assert.Equal(2, pageTwo.PageSize);
        Assert.Single(pageTwo.Items);
        Assert.Empty(pageOne.Items.Select(machine => machine.Id).Intersect(pageTwo.Items.Select(machine => machine.Id)));
    }

    [Theory]
    [InlineData("code")]
    [InlineData("asset")]
    [InlineData("name")]
    [InlineData("manufacturer")]
    [InlineData("model")]
    [InlineData("serial")]
    public async Task Search_covers_all_six_machine_identifiers_case_insensitively(string field)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        var target = await fixture.SeedMachineAsync(configure: machine =>
        {
            switch (field)
            {
                case "code": machine.MachineCode = "NEEDLE-CODE"; break;
                case "asset": machine.AssetNumber = "NEEDLE-ASSET"; break;
                case "name": machine.Name = "NEEDLE-NAME"; break;
                case "manufacturer": machine.Manufacturer = "NEEDLE-MANUFACTURER"; break;
                case "model": machine.Model = "NEEDLE-MODEL"; break;
                case "serial": machine.SerialNumber = "NEEDLE-SERIAL"; break;
            }
        });
        await fixture.SeedMachineAsync();

        var results = await fixture.Machines.ListAsync(new MachineQuery { Search = "needle" });

        Assert.Equal(target.Id, Assert.Single(results.Items).Id);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Invalid_pagination_is_rejected(int page, int pageSize)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();

        await ModuleFixture.ExpectStatusAsync(400,
            () => fixture.Machines.ListAsync(new MachineQuery { Page = page, PageSize = pageSize }));
    }

    [Theory]
    [InlineData("TECHNICIAN")]
    [InlineData("SUPERVISOR")]
    public async Task Technician_and_supervisor_cannot_mutate_machines(string role)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        fixture.ActAs(await fixture.SeedUserAsync(role));

        await ModuleFixture.ExpectStatusAsync(403, () => fixture.Machines.CreateAsync(NewMachine()));
    }

    private static MachineWriteRequest NewMachine(string code = "PUMP-001") =>
        new() { MachineCode = code, Name = "Test pump" };

    private sealed class AuditUnavailableException : Exception { }

    private sealed class RejectingAuditWriter : IAuditWriter
    {
        public void Record(string action, string entityType, Guid? entityId,
            object? oldValues = null, object? newValues = null) => throw new AuditUnavailableException();
    }
}
