using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class ChecklistTemplateConfiguration : IEntityTypeConfiguration<ChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<ChecklistTemplate> builder)
    {
        builder.ToTable("ChecklistTemplates");
        builder.HasKey(x => x.Id);
        builder.ToTable(t => t.HasCheckConstraint("CK_ChecklistTemplates_Version", "\"Version\" > 0"));
        builder.Property(x => x.Name).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.MaintenancePlanId, x.Version }).IsUnique();
        builder.HasOne(x => x.MaintenancePlan).WithMany(x => x.ChecklistTemplates).HasForeignKey(x => x.MaintenancePlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
