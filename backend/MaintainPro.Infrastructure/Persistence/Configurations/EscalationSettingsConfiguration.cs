using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class EscalationSettingsConfiguration : IEntityTypeConfiguration<EscalationSettings>
{
    public void Configure(EntityTypeBuilder<EscalationSettings> b)
    {
        b.ToTable("EscalationSettings");
        b.HasKey(x => x.Id);
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.Version).IsConcurrencyToken();
        b.Property(x => x.Id).ValueGeneratedNever();
        b.ToTable(t => t.HasCheckConstraint("CK_EscalationSettings_SingletonAndThresholds", "\"Id\" = 1 AND \"DueSoonDays\" BETWEEN 0 AND 365 AND \"TechnicianOverdueDays\" >= 0 AND \"SupervisorEscalationDays\" >= \"TechnicianOverdueDays\" AND \"ManagerEscalationDays\" >= \"SupervisorEscalationDays\" AND \"ManagerEscalationDays\" <= 36500"));
        b.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
