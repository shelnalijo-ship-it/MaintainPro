using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderNumberSequenceConfiguration : IEntityTypeConfiguration<WorkOrderNumberSequence>
{
    public void Configure(EntityTypeBuilder<WorkOrderNumberSequence> builder)
    {
        builder.ToTable("WorkOrderNumberSequences", t => { t.HasCheckConstraint("CK_WorkOrderNumberSequences_Year", "\"Year\" >= 1 AND \"Year\" <= 9999"); t.HasCheckConstraint("CK_WorkOrderNumberSequences_Value", "\"LastValue\" >= 0"); });
        builder.HasKey(x => x.Year);
        builder.Property(x => x.Year).ValueGeneratedNever();
        builder.Property(x => x.Version).IsConcurrencyToken();
    }
}
