using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="TableTemplate"/> entity.
/// </summary>
public sealed class TableTemplateConfiguration : IEntityTypeConfiguration<TableTemplate>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TableTemplate> builder)
    {
        builder.ConfigureGuidV7Key();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(TableTemplateValidationConstants.MaxNameLength);

        builder.Property(t => t.Description)
            .HasMaxLength(TableTemplateValidationConstants.MaxDescriptionLength);

        builder.Property(t => t.IsSystemPreset)
            .IsRequired();

        builder.ConfigureAuthorTracking(t => t.Creator, t => t.Updater);

        builder.HasMany(t => t.Fields)
            .WithOne(f => f.Template)
            .HasForeignKey(f => f.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
