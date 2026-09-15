using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class WorkOrderChecklistResultConfiguration : IEntityTypeConfiguration<WorkOrderChecklistResult>
{
    public void Configure(EntityTypeBuilder<WorkOrderChecklistResult> b)
    {
        b.ToTable("WorkOrderChecklistResults");
        b.HasKey(x => x.Id);
        b.Property(x => x.NumericValue).HasColumnType("numeric");
        b.Property(x => x.PassFailValue).HasConversion<string>();
        b.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.WorkOrderId, x.WorkOrderChecklistItemId }).IsUnique();
        b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.WorkOrderChecklistItem).WithMany().HasForeignKey(x => x.WorkOrderChecklistItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CompletedByUser).WithMany().HasForeignKey(x => x.CompletedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
