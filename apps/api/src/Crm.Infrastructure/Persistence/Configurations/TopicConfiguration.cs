using Crm.Domain.Consts.Entities.ValidationConstants;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework mapping for the <see cref="Topic"/> entity.
/// </summary>
public class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

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
    }
}
