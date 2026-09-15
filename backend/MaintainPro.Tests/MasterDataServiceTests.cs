using MaintainPro.Application.MasterData;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class MasterDataServiceTests
{
    [Fact]
    public async Task Department_category_and_location_support_create_edit_and_soft_deactivation()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        fixture.ActAs(await fixture.SeedUserAsync("MANAGER"));

        var department = await fixture.Masters.CreateDepartmentAsync(new MasterDataWriteRequest(" Production ", "First"));
        var category = await fixture.Masters.CreateMachineCategoryAsync(new MasterDataWriteRequest(" Pumps "));
        var location = await fixture.Masters.CreateLocationAsync(new LocationWriteRequest(" Floor 1 ", DepartmentId: department.Id));
        await fixture.Masters.UpdateDepartmentAsync(department.Id, new MasterDataWriteRequest("Production", "Updated"));
        await fixture.Masters.UpdateMachineCategoryAsync(category.Id, new MasterDataWriteRequest("Pumps", "Updated"));
        await fixture.Masters.UpdateLocationAsync(location.Id, new LocationWriteRequest("Floor 1", "Updated", department.Id));
        await fixture.Masters.SetDepartmentStatusAsync(department.Id, new MasterDataStatusRequest(false));
        await fixture.Masters.SetMachineCategoryStatusAsync(category.Id, new MasterDataStatusRequest(false));
        await fixture.Masters.SetLocationStatusAsync(location.Id, new MasterDataStatusRequest(false));

        Assert.Equal("Production", department.Name);
        Assert.False(Assert.Single(await fixture.Masters.ListDepartmentsAsync()).IsActive);
        Assert.False(Assert.Single(await fixture.Masters.ListMachineCategoriesAsync()).IsActive);
        Assert.False(Assert.Single(await fixture.Masters.ListLocationsAsync(departmentId: department.Id)).IsActive);
        Assert.Empty(await fixture.Masters.ListDepartmentsAsync(true));
        Assert.Empty(await fixture.Masters.ListMachineCategoriesAsync(true));
        Assert.Empty(await fixture.Masters.ListLocationsAsync(true));
        Assert.Equal(1, await fixture.Db.Departments.CountAsync());
        Assert.Equal(1, await fixture.Db.MachineCategories.CountAsync());
        Assert.Equal(1, await fixture.Db.Locations.CountAsync());
        Assert.Contains(await fixture.Db.AuditLogs.ToListAsync(), log => log.Action == "Department.StatusChanged");
    }

    [Fact]
    public async Task Master_names_are_unique_after_case_and_whitespace_normalization()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        await fixture.Masters.CreateDepartmentAsync(new MasterDataWriteRequest("Production"));
        await fixture.Masters.CreateMachineCategoryAsync(new MasterDataWriteRequest("Pumps"));
        await fixture.Masters.CreateLocationAsync(new LocationWriteRequest("Workshop"));

        await ModuleFixture.ExpectStatusAsync(409,
            () => fixture.Masters.CreateDepartmentAsync(new MasterDataWriteRequest(" production ")));
        await ModuleFixture.ExpectStatusAsync(409,
            () => fixture.Masters.CreateMachineCategoryAsync(new MasterDataWriteRequest(" pumps ")));
        await ModuleFixture.ExpectStatusAsync(409,
            () => fixture.Masters.CreateLocationAsync(new LocationWriteRequest(" workshop ")));
    }

    [Fact]
    public async Task Location_department_must_exist_and_cannot_be_moved_across_existing_machine_references()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await fixture.AsAdminAsync();
        await ModuleFixture.ExpectStatusAsync(400, () => fixture.Masters.CreateLocationAsync(
            new LocationWriteRequest("Unknown department", DepartmentId: Guid.NewGuid())));
        var first = await fixture.Masters.CreateDepartmentAsync(new MasterDataWriteRequest("First"));
        var second = await fixture.Masters.CreateDepartmentAsync(new MasterDataWriteRequest("Second"));
        var location = await fixture.Masters.CreateLocationAsync(new LocationWriteRequest("Workshop", DepartmentId: first.Id));
        await fixture.SeedMachineAsync(configure: machine =>
        {
            machine.DepartmentId = first.Id;
            machine.LocationId = location.Id;
        });

        await ModuleFixture.ExpectStatusAsync(409, () => fixture.Masters.UpdateLocationAsync(location.Id,
            new LocationWriteRequest("Workshop", DepartmentId: second.Id)));

        Assert.Equal(first.Id, (await fixture.Db.Locations.SingleAsync()).DepartmentId);
    }

    [Theory]
    [InlineData("TECHNICIAN")]
    [InlineData("SUPERVISOR")]
    public async Task Operational_roles_cannot_mutate_master_data(string role)
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        fixture.ActAs(await fixture.SeedUserAsync(role));

        await ModuleFixture.ExpectStatusAsync(403,
            () => fixture.Masters.CreateDepartmentAsync(new MasterDataWriteRequest("Production")));
        await ModuleFixture.ExpectStatusAsync(403,
            () => fixture.Masters.CreateMachineCategoryAsync(new MasterDataWriteRequest("Pumps")));
        await ModuleFixture.ExpectStatusAsync(403,
            () => fixture.Masters.CreateLocationAsync(new LocationWriteRequest("Workshop")));
    }
}
