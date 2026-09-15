using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderSubmissionConfiguration : IEntityTypeConfiguration<WorkOrderSubmission>
{
    public void Configure(EntityTypeBuilder<WorkOrderSubmission> b)
    {
        b.ToTable("WorkOrderSubmissions");
        b.HasKey(x => x.Id);
        b.Property(x => x.TechnicianEmployeeId).IsRequired();
        b.Property(x => x.TechnicianName).IsRequired();
        b.Property(x => x.SubmittedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.StartedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.DurationMinutes).HasColumnType("numeric");
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.WorkOrderId, x.VersionNumber }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_WorkOrderSubmissions_VersionAndDuration", "\"VersionNumber\" > 0 AND \"DurationMinutes\" >= 0 AND \"CompletedAt\" >= \"StartedAt\" AND \"SubmittedAt\" >= \"CompletedAt\""));
        b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SubmittedByUser).WithMany().HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
