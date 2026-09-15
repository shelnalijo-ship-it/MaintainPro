using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintainPro.Infrastructure.Persistence.Configurations;

public sealed class CorrectiveSubmissionAttachmentConfiguration : IEntityTypeConfiguration<CorrectiveSubmissionAttachment>
{
    public void Configure(EntityTypeBuilder<CorrectiveSubmissionAttachment> b)
    {
        b.ToTable("CorrectiveSubmissionAttachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.UploadedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.EvidenceType).HasConversion<string>();
        b.HasIndex(x => new { x.CorrectiveSubmissionId, x.FileId }).IsUnique();
        b.HasOne(x => x.Submission).WithMany(x => x.Attachments).HasForeignKey(x => x.CorrectiveSubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

