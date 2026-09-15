using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MaintainPro.Domain.Entities;

namespace MaintainPro.Tests;

public sealed class ReportingIndexTests
{
    [Fact]
    public async Task Reporting_scan_paths_have_supporting_indexes_without_changing_delete_behavior()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var model = fixture.Db.Model;

        HasIndex<WorkOrder>(model, nameof(WorkOrder.DueDate));
        HasIndex<Breakdown>(model, nameof(Breakdown.ReportedAt));
        HasIndex<ExternalService>(model, nameof(ExternalService.ServiceDate));
        HasIndex<Machine>(model, nameof(Machine.DepartmentId), nameof(Machine.LocationId), nameof(Machine.Status));
        HasIndex<MachineAssignmentHistory>(model, nameof(MachineAssignmentHistory.MachineId),
            nameof(MachineAssignmentHistory.EffectiveFrom));
        HasIndex<AuditLog>(model, nameof(AuditLog.EntityType), nameof(AuditLog.EntityId), nameof(AuditLog.CreatedAt));
        Assert.All(model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()),
            x => Assert.Equal(DeleteBehavior.Restrict, x.DeleteBehavior));
    }

    private static void HasIndex<T>(IModel model, params string[] properties)
    {
        var entity = model.FindEntityType(typeof(T))!;
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(x => x.Name).SequenceEqual(properties));
    }
}
