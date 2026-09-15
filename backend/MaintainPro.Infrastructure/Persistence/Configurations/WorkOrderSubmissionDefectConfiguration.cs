using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderSubmissionDefectConfiguration : IEntityTypeConfiguration<WorkOrderSubmissionDefect>
{
    public void Configure(EntityTypeBuilder<WorkOrderSubmissionDefect> b)
    {
        b.ToTable("WorkOrderSubmissionDefects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired();
        b.Property(x => x.Description).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.HasOne(x => x.Submission).WithMany(x => x.Defects).HasForeignKey(x => x.WorkOrderSubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
