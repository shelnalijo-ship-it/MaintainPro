using MaintainPro.Application.ExternalServices;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MaintainPro.Tests;

public sealed class ExternalServiceTests
{
    [Fact]
    public async Task Create_captures_complete_service_and_machine_snapshot_with_audit()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);

        var created = await fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data,
            followUp: data.Today.AddDays(5), nextService: data.Today.AddDays(180)));

        Assert.Matches($"^ES-{data.Today.Year:D4}-0001$", created.ServiceNumber);
        Assert.Equal(data.Machine.MachineCode, created.MachineCode);
        Assert.Equal(data.Machine.Name, created.MachineName);
        Assert.Equal(1250.50m, created.Cost);
        Assert.Equal(data.Manager.Id, created.CreatedByUserId);
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), x => x.Action == "ExternalService.Created");
    }

    [Fact]
    public async Task Creation_rejects_invalid_machine_cost_dates_and_enum()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        var valid = ExternalServiceTestData.Request(data);

        await ModuleFixture.ExpectStatusAsync(404,
            () => fixture.ExternalServices.CreateAsync(valid with { MachineId = Guid.NewGuid() }));
        await ModuleFixture.ExpectStatusAsync(400,
            () => fixture.ExternalServices.CreateAsync(valid with { Cost = -0.01m }));
        await ModuleFixture.ExpectStatusAsync(400,
            () => fixture.ExternalServices.CreateAsync(valid with { ServiceDate = data.Today.AddDays(1) }));
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.ExternalServices.CreateAsync(valid with
        {
            FollowUpDate = data.Today.AddDays(-3)
        }));
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.ExternalServices.CreateAsync(valid with
        {
            ServiceType = (ExternalServiceType)999
        }));
        Assert.Empty(await fixture.Db.ExternalServices.ToListAsync());
    }

    [Fact]
    public async Task Technician_and_supervisor_create_only_for_their_machine_and_role_union_is_preserved()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        var hidden = await fixture.SeedMachineAsync();
        fixture.ActAs(data.Technician);
        var technicianEntry = ExternalServiceTestData.Request(data) with
        {
            Cost = null, PurchaseOrderNumber = null, InvoiceNumber = null
        };
        Assert.NotNull(await fixture.ExternalServices.CreateAsync(technicianEntry));
        await ModuleFixture.ExpectStatusAsync(404, () => fixture.ExternalServices.CreateAsync(
            technicianEntry with { MachineId = hidden.Id }));
        await ModuleFixture.ExpectStatusAsync(403,
            () => fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data)));

        fixture.ActAs(data.Supervisor);
        Assert.NotNull(await fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data) with
        {
            Description = "Supervisor entry"
        }));

        var multiRole = await fixture.SeedUserAsync("TECHNICIAN", "SUPERVISOR");
        var owned = await fixture.SeedMachineAsync(multiRole.Id, data.Supervisor.Id);
        var supervised = await fixture.SeedMachineAsync(data.Technician.Id, multiRole.Id);
        fixture.ActAs(multiRole);
        Assert.NotNull(await fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data) with
        {
            MachineId = owned.Id, Description = "Owned"
        }));
        Assert.NotNull(await fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data) with
        {
            MachineId = supervised.Id, Description = "Supervised"
        }));
    }

    [Fact]
    public async Task Correction_is_audited_and_preserves_number_machine_creator_and_snapshot()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        var created = await fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data));
        data.Machine.Name = "Renamed current machine";
        await fixture.Db.SaveChangesAsync();

        fixture.ActAs(data.Supervisor);
        var corrected = await fixture.ExternalServices.UpdateAsync(created.Id,
            ExternalServiceTestData.Request(data) with { Findings = "Corrected finding", Cost = 1300m });

        Assert.Equal(created.ServiceNumber, corrected.ServiceNumber);
        Assert.Equal(created.MachineId, corrected.MachineId);
        Assert.Equal(created.MachineName, corrected.MachineName);
        Assert.Equal(created.CreatedByUserId, corrected.CreatedByUserId);
        Assert.Equal("Corrected finding", corrected.Findings);
        var audit = Assert.Single(await fixture.Db.AuditLogs.Where(x => x.Action == "ExternalService.Corrected").ToListAsync());
        using var oldAudit = JsonDocument.Parse(audit.OldValuesJson!);
        using var newAudit = JsonDocument.Parse(audit.NewValuesJson!);
        Assert.Equal(1250.50m, oldAudit.RootElement.GetProperty("Cost").GetDecimal());
        Assert.Equal(1300m, newAudit.RootElement.GetProperty("Cost").GetDecimal());
        await ModuleFixture.ExpectStatusAsync(409, () => fixture.ExternalServices.UpdateAsync(created.Id,
            ExternalServiceTestData.Request(data) with { MachineId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task Technician_cannot_correct_history_but_manager_and_assigned_supervisor_can()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        var created = await fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data));
        fixture.ActAs(data.Technician);
        await ModuleFixture.ExpectStatusAsync(403,
            () => fixture.ExternalServices.UpdateAsync(created.Id, ExternalServiceTestData.Request(data)));
        fixture.ActAs(data.Administrator);
        Assert.NotNull(await fixture.ExternalServices.UpdateAsync(created.Id,
            ExternalServiceTestData.Request(data) with { Comments = "Admin correction" }));
    }

    [Fact]
    public async Task Search_filters_pagination_and_machine_history_use_authorized_scope()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        var first = await fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data));
        await fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data, "Second Provider", "Inspection") with
        {
            ServiceType = ExternalServiceType.INSPECTION, PurchaseOrderNumber = "PO-SEARCH",
            InvoiceNumber = "INV-SEARCH"
        });
        var outsider = await fixture.SeedUserAsync("TECHNICIAN");
        var otherMachine = await fixture.SeedMachineAsync(outsider.Id);
        await fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data, "Hidden Provider") with
        {
            MachineId = otherMachine.Id
        });

        fixture.ActAs(data.Technician);
        var page = await fixture.ExternalServices.ListAsync(new(Search: "PO-SEARCH", Page: 1, PageSize: 1));
        Assert.Equal(1, page.TotalCount);
        Assert.Equal("Second Provider", Assert.Single(page.Items).ServiceCompany);
        var history = await fixture.ExternalServices.MachineHistoryAsync(data.Machine.Id, new(PageSize: 1));
        Assert.Equal(2, history.TotalCount);
        Assert.Single(history.Items);
        await ModuleFixture.ExpectStatusAsync(404,
            () => fixture.ExternalServices.MachineHistoryAsync(otherMachine.Id, new()));
        Assert.Equal(first.Id, Assert.Single((await fixture.ExternalServices.ListAsync(
            new(InvoiceNumber: "INV-100"))).Items).Id);
    }

    [Fact]
    public async Task Physical_deletion_and_identity_rewrites_are_blocked()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        var created = await fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data));
        fixture.Db.ChangeTracker.Clear();
        var service = await fixture.Db.ExternalServices.SingleAsync(x => x.Id == created.Id);
        fixture.Db.ExternalServices.Remove(service);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.Entry(service).State = EntityState.Unchanged;
        service.ServiceNumber = "ES-2000-9999";
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Follow_up_query_supports_upcoming_overdue_company_and_machine_filters()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        var overdue = await fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data,
            followUp: data.Today.AddDays(-1)));
        await fixture.ExternalServices.CreateAsync(ExternalServiceTestData.Request(data, "Future Provider",
            followUp: data.Today.AddDays(10)));

        var overduePage = await fixture.ExternalServices.FollowUpsAsync(new(OverdueOnly: true));
        Assert.Equal(overdue.Id, Assert.Single(overduePage.Items).ExternalServiceId);
        var upcoming = await fixture.ExternalServices.FollowUpsAsync(new(DueBefore: data.Today.AddDays(10),
            ServiceCompany: "future", MachineId: data.Machine.Id));
        Assert.Contains(upcoming.Items, x => x.ServiceCompany == "Future Provider" && !x.IsOverdue);
        fixture.ActAs(data.Technician);
        await ModuleFixture.ExpectStatusAsync(403, () => fixture.ExternalServices.FollowUpsAsync(new()));
    }
}
