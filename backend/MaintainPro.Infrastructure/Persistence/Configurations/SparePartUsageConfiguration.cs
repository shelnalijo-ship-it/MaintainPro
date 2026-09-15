using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class SparePartUsageConfiguration : IEntityTypeConfiguration<SparePartUsage>
{
    public void Configure(EntityTypeBuilder<SparePartUsage> b)
    {
        b.ToTable("SparePartUsages");
        b.HasKey(x => x.Id);
        b.Property(x => x.PartName).IsRequired();
        b.Property(x => x.Quantity).HasColumnType("numeric");
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.ToTable(t => t.HasCheckConstraint("CK_SparePartUsages_Quantity", "\"Quantity\" > 0"));
        b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
