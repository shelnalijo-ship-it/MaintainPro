using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        builder.ToTable("ChecklistItems");
        builder.HasKey(x => x.Id);
        builder.ToTable(t => { t.HasCheckConstraint("CK_ChecklistItems_Sequence", "\"SequenceNumber\" > 0"); t.HasCheckConstraint("CK_ChecklistItems_Bounds", "\"MinimumValue\" IS NULL OR \"MaximumValue\" IS NULL OR \"MinimumValue\" <= \"MaximumValue\""); });
        builder.Property(x => x.Title).IsRequired();
        builder.Property(x => x.ResponseType).HasConversion<string>();
        builder.Property(x => x.MinimumValue).HasColumnType("numeric");
        builder.Property(x => x.MaximumValue).HasColumnType("numeric");
        builder.HasIndex(x => new { x.ChecklistTemplateId, x.SequenceNumber }).IsUnique();
        builder.HasOne(x => x.ChecklistTemplate).WithMany(x => x.Items).HasForeignKey(x => x.ChecklistTemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}
