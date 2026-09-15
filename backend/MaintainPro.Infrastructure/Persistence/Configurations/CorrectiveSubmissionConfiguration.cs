using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class CorrectiveSubmissionConfiguration : IEntityTypeConfiguration<CorrectiveSubmission>
{
    public void Configure(EntityTypeBuilder<CorrectiveSubmission> b)
    {
        b.ToTable("CorrectiveSubmissions");
        b.HasKey(x => x.Id);
        b.Property(x => x.TechnicianEmployeeId).IsRequired();
        b.Property(x => x.TechnicianName).IsRequired();
        b.Property(x => x.RootCause).IsRequired();
        b.Property(x => x.CorrectiveAction).IsRequired();
        b.Property(x => x.StartedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.SubmittedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.DurationMinutes).HasColumnType("numeric");
        b.Property(x => x.DowntimeMinutes).HasColumnType("numeric");
        b.HasIndex(x => new { x.BreakdownId, x.VersionNumber }).IsUnique();
        b.ToTable(t => { t.HasCheckConstraint("CK_CorrectiveSubmissions_Version", "\"VersionNumber\" > 0"); t.HasCheckConstraint("CK_CorrectiveSubmissions_Durations", "\"DurationMinutes\" >= 0 AND \"DowntimeMinutes\" >= 0"); t.HasCheckConstraint("CK_CorrectiveSubmissions_Times", "\"CompletedAt\" >= \"StartedAt\" AND \"SubmittedAt\" >= \"CompletedAt\""); });
        b.HasOne(x => x.Breakdown).WithMany().HasForeignKey(x => x.BreakdownId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Technician).WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
    }
}

