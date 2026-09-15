using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class CorrectiveSubmissionPartUsageConfiguration : IEntityTypeConfiguration<CorrectiveSubmissionPartUsage>
{
    public void Configure(EntityTypeBuilder<CorrectiveSubmissionPartUsage> b)
    {
        b.ToTable("CorrectiveSubmissionPartUsages");
        b.HasKey(x => x.Id);
        b.Property(x => x.PartName).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.Quantity).HasColumnType("numeric");
        b.ToTable(t => t.HasCheckConstraint("CK_CorrectiveSubmissionPartUsages_Quantity", "\"Quantity\" > 0"));
        b.HasOne(x => x.Submission).WithMany(x => x.PartUsages).HasForeignKey(x => x.CorrectiveSubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

