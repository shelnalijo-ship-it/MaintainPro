using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderDefinitionConfiguration : IEntityTypeConfiguration<WorkOrderDefinition>
{
    public void Configure(EntityTypeBuilder<WorkOrderDefinition> builder)
    {
        builder.ToTable("WorkOrderDefinitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Priority).HasConversion<string>();
        builder.HasIndex(x => x.WorkOrderId).IsUnique();
        builder.HasOne(x => x.WorkOrder).WithOne(x => x.Definition).HasForeignKey<WorkOrderDefinition>(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MaintenanceType).WithMany().HasForeignKey(x => x.MaintenanceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ChecklistTemplate).WithMany().HasForeignKey(x => x.ChecklistTemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}
