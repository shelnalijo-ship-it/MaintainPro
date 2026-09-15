using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class ExternalServiceConfiguration : IEntityTypeConfiguration<ExternalService>
{
    public void Configure(EntityTypeBuilder<ExternalService> b)
    {
        b.ToTable("ExternalServices");
        b.HasKey(x => x.Id);
        b.Property(x => x.ServiceNumber).HasMaxLength(32).IsRequired();
        b.Property(x => x.MachineCode).HasMaxLength(100).IsRequired();
        b.Property(x => x.MachineName).HasMaxLength(200).IsRequired();
        b.Property(x => x.ServiceCompany).HasMaxLength(300).IsRequired();
        b.Property(x => x.ServiceTechnician).HasMaxLength(200);
        b.Property(x => x.ServiceType).HasConversion<string>();
        b.Property(x => x.Description).HasMaxLength(10000).IsRequired();
        b.Property(x => x.Findings).HasMaxLength(10000);
        b.Property(x => x.WorkCompleted).HasMaxLength(10000);
        b.Property(x => x.PartsReplaced).HasMaxLength(10000);
        b.Property(x => x.Cost).HasPrecision(18, 2);
        b.Property(x => x.PurchaseOrderNumber).HasMaxLength(200);
        b.Property(x => x.InvoiceNumber).HasMaxLength(200);
        b.Property(x => x.Recommendation).HasMaxLength(10000);
        b.Property(x => x.Comments).HasMaxLength(10000);
        b.Property(x => x.ServiceDate).HasColumnType("date");
        b.Property(x => x.FollowUpDate).HasColumnType("date");
        b.Property(x => x.NextServiceDate).HasColumnType("date");
        b.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.ServiceNumber).IsUnique();
        b.HasIndex(x => new { x.MachineId, x.ServiceDate });
        b.HasIndex(x => x.ServiceCompany);
        b.HasIndex(x => x.FollowUpDate);
        b.HasIndex(x => x.NextServiceDate);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ExternalServices_Cost", "\"Cost\" IS NULL OR \"Cost\" >= 0");
            t.HasCheckConstraint("CK_ExternalServices_Dates",
                "(\"FollowUpDate\" IS NULL OR \"FollowUpDate\" >= \"ServiceDate\") AND " +
                "(\"NextServiceDate\" IS NULL OR \"NextServiceDate\" >= \"ServiceDate\")");
        });
        b.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
