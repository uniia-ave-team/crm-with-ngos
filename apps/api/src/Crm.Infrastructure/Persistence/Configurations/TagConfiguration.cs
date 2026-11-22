using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the project-scoped <see cref="Tag"/> entity.
/// </summary>
public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ConfigureGuidV7Key();

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(ProjectValidationConstants.MaxTagTitleLength);

        builder.Property(t => t.NormalizedTitle)
            .IsRequired()
            .HasMaxLength(ProjectValidationConstants.MaxTagTitleLength);

        builder.HasIndex(t => new { t.ProjectId, t.NormalizedTitle })
            .IsUnique();

        builder.HasQueryFilter(t => !t.Project.IsDeleted);
    }
}
