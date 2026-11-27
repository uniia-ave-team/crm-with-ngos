using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="TableTemplate"/> entity,
/// storing its column blueprints directly as a PostgreSQL JSONB owned collection.
/// </summary>
public sealed class TableTemplateConfiguration : IEntityTypeConfiguration<TableTemplate>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TableTemplate> builder)
    {
        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_TableTemplate_Fields_IsArray",
                $"\"{nameof(TableTemplate.Fields)}\" IS NOT NULL AND jsonb_typeof(\"{nameof(TableTemplate.Fields)}\") = 'array'"));

        builder.ConfigureGuidV7Key();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(CustomTableValidationConstants.MaxNameLength);

        builder.Property(t => t.Description)
            .HasMaxLength(CustomTableValidationConstants.MaxDescriptionLength);

        builder.Property(t => t.IsSystemPreset)
            .IsRequired();

        builder.ConfigureAuthorTracking(t => t.Creator, t => t.Updater);

        builder.OwnsMany(t => t.Fields, nav => nav.ToJson());
    }
}
