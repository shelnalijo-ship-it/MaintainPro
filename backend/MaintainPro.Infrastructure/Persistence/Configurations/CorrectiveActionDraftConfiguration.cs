using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class CorrectiveActionDraftConfiguration : IEntityTypeConfiguration<CorrectiveActionDraft>
{
    public void Configure(EntityTypeBuilder<CorrectiveActionDraft> b)
    {
        b.ToTable("CorrectiveActionDrafts");
        b.HasKey(x => x.Id);
        b.Property(x => x.TechnicianEmployeeId).IsRequired();
        b.Property(x => x.TechnicianName).IsRequired();
        b.Property(x => x.AttemptStartedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.AccumulatedDurationMinutes).HasColumnType("numeric");
        b.HasIndex(x => x.BreakdownId).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_CorrectiveActionDrafts_Duration", "\"AccumulatedDurationMinutes\" >= 0"));
        b.HasOne(x => x.Breakdown).WithMany().HasForeignKey(x => x.BreakdownId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Technician).WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
    }
}

