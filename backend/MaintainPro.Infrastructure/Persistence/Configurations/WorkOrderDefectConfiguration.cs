using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderDefectConfiguration : IEntityTypeConfiguration<WorkOrderDefect>
{
    public void Configure(EntityTypeBuilder<WorkOrderDefect> b)
    {
        b.ToTable("WorkOrderDefects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired();
        b.Property(x => x.Description).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
