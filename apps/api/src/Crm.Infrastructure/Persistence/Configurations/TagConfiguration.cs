using Crm.Domain.Consts.Entities.ValidationConstants;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework mapping for the project-scoped <see cref="Tag"/> entity.
/// </summary>
public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(ProjectValidationConstants.MaxTagTitleLength);

        builder.Property(t => t.NormalizedTitle)
            .IsRequired()
            .HasMaxLength(ProjectValidationConstants.MaxTagTitleLength);

        builder.HasIndex(t => new { t.ProjectId, t.NormalizedTitle })
            .IsUnique();
    }
}
