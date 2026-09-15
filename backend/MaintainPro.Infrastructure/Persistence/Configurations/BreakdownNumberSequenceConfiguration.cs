using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class BreakdownNumberSequenceConfiguration : IEntityTypeConfiguration<BreakdownNumberSequence>
{
    public void Configure(EntityTypeBuilder<BreakdownNumberSequence> b)
    {
        b.ToTable("BreakdownNumberSequences");
        b.HasKey(x => x.Year);
        b.Property(x => x.Year).ValueGeneratedNever();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.ToTable(t => { t.HasCheckConstraint("CK_BreakdownNumberSequences_Year", "\"Year\" BETWEEN 1 AND 9999"); t.HasCheckConstraint("CK_BreakdownNumberSequences_Value", "\"LastValue\" >= 0"); });
    }
}

