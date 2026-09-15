using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class CorrectivePartUsageConfiguration : IEntityTypeConfiguration<CorrectivePartUsage>
{
    public void Configure(EntityTypeBuilder<CorrectivePartUsage> b)
    {
        b.ToTable("CorrectivePartUsages");
        b.HasKey(x => x.Id);
        b.Property(x => x.PartName).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.Quantity).HasColumnType("numeric");
        b.ToTable(t => t.HasCheckConstraint("CK_CorrectivePartUsages_Quantity", "\"Quantity\" > 0"));
        b.HasOne(x => x.Breakdown).WithMany().HasForeignKey(x => x.BreakdownId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

