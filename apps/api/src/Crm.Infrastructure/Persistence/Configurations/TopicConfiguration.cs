using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="Topic"/> entity.
/// </summary>
public sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ConfigureGuidV7Key();

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(ProjectValidationConstants.MaxTopicTitleLength);

        builder.Property(t => t.NormalizedTitle)
            .IsRequired()
            .HasMaxLength(ProjectValidationConstants.MaxTopicTitleLength);

        builder.Property(t => t.Order)
            .IsRequired();

        builder.HasIndex(t => new { t.ProjectId, t.Order })
            .IsUnique();
        builder.HasIndex(t => new { t.ProjectId, t.NormalizedTitle })
            .IsUnique();

        builder.HasQueryFilter(t => !t.Project.IsDeleted);
    }
}
