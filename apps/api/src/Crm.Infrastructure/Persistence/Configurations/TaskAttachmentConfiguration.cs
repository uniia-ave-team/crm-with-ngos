using Crm.Domain.Consts.Entities.ValidationConstants;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework mapping for the <see cref="TaskAttachment"/> entity.
/// </summary>
public class TaskAttachmentConfiguration : IEntityTypeConfiguration<TaskAttachment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TaskAttachment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

        builder.Property(a => a.OriginalFileName)
            .IsRequired()
            .HasMaxLength(TaskValidationConstants.MaxAttachmentFileNameLength);

        builder.Property(a => a.StoredFileName)
            .IsRequired()
            .HasMaxLength(TaskValidationConstants.MaxAttachmentStoredFileNameLength);

        builder.Property(a => a.ContentType)
            .IsRequired()
            .HasMaxLength(TaskValidationConstants.MaxAttachmentContentTypeLength);

        builder.Property(a => a.FileSize)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.HasOne(a => a.Uploader)
            .WithMany()
            .HasForeignKey(a => a.UploadedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => a.TaskId);
        builder.HasIndex(a => a.UploadedBy);
    }
}
