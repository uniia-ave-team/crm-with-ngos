using Crm.Domain.Consts.Entities.ValidationConstants;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework mapping for the <see cref="TaskComment"/> entity.
/// </summary>
public class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.ToTable(TableNames.TaskComment);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

        builder.Property(c => c.Content)
            .IsRequired()
            .HasMaxLength(TaskValidationConstants.MaxCommentContentLength);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired();

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => new { c.TaskId, c.CreatedAt });
    }
}
