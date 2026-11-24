using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="TaskAttachment"/> entity.
/// </summary>
public sealed class TaskAttachmentConfiguration : IEntityTypeConfiguration<TaskAttachment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TaskAttachment> builder)
    {
        builder.ConfigureGuidV7Key();

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

        builder.HasQueryFilter(a => !a.Task.IsDeleted && !a.Task.Project.IsDeleted);
    }
}
