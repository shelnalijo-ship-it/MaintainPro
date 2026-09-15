using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderChecklistItemConfiguration : IEntityTypeConfiguration<WorkOrderChecklistItem>
{
    public void Configure(EntityTypeBuilder<WorkOrderChecklistItem> builder)
    {
        builder.ToTable("WorkOrderChecklistItems");
        builder.HasKey(x => x.Id);
        builder.ToTable(t => { t.HasCheckConstraint("CK_WorkOrderChecklistItems_Sequence", "\"SequenceNumber\" > 0"); t.HasCheckConstraint("CK_WorkOrderChecklistItems_Bounds", "\"MinimumValue\" IS NULL OR \"MaximumValue\" IS NULL OR \"MinimumValue\" <= \"MaximumValue\""); });
        builder.Property(x => x.Title).IsRequired();
        builder.Property(x => x.ResponseType).HasConversion<string>();
        builder.Property(x => x.MinimumValue).HasColumnType("numeric");
        builder.Property(x => x.MaximumValue).HasColumnType("numeric");
        builder.HasIndex(x => new { x.WorkOrderDefinitionId, x.SequenceNumber }).IsUnique();
        builder.HasOne(x => x.Definition).WithMany(x => x.Items).HasForeignKey(x => x.WorkOrderDefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}
