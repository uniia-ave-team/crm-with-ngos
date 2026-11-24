using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="Project"/> entity.
/// </summary>
public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ConfigureGuidV7Key();

        builder.Property(p => p.Title)
            .IsRequired()
            .HasMaxLength(ProjectValidationConstants.MaxTitleLength);

        builder.Property(p => p.Description)
            .HasMaxLength(ProjectValidationConstants.MaxDescriptionLength);

        builder.Property(p => p.Visibility)
            .IsRequired();

        builder.Property(p => p.StartDate)
            .IsRequired();

        builder.ConfigureAuthorTracking(p => p.Creator, p => p.Updater);
        builder.ConfigureSoftDelete();

        builder.HasMany(p => p.Members)
            .WithOne(m => m.Project)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Topics)
            .WithOne(t => t.Project)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Tasks)
            .WithOne(t => t.Project)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Tags)
            .WithOne(t => t.Project)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.Visibility);
    }
}
