using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderSubmissionPartUsageConfiguration : IEntityTypeConfiguration<WorkOrderSubmissionPartUsage>
{
    public void Configure(EntityTypeBuilder<WorkOrderSubmissionPartUsage> b)
    {
        b.ToTable("WorkOrderSubmissionPartUsages");
        b.HasKey(x => x.Id);
        b.Property(x => x.PartName).IsRequired();
        b.Property(x => x.Quantity).HasColumnType("numeric");
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.ToTable(t => t.HasCheckConstraint("CK_WorkOrderSubmissionPartUsages_Quantity", "\"Quantity\" > 0"));
        b.HasOne(x => x.Submission).WithMany(x => x.PartUsages).HasForeignKey(x => x.WorkOrderSubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
