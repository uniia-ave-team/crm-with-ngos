using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="CustomTable"/> entity.
/// </summary>
public sealed class CustomTableConfiguration : IEntityTypeConfiguration<CustomTable>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CustomTable> builder)
    {
        builder.ConfigureGuidV7Key();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(CustomTableValidationConstants.MaxNameLength);

        builder.Property(t => t.Description)
            .HasMaxLength(CustomTableValidationConstants.MaxDescriptionLength);

        builder.HasOne(t => t.Ngo)
            .WithMany(n => n.CustomTables)
            .HasForeignKey(t => t.NgoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.SourceTemplate)
            .WithMany()
            .HasForeignKey(t => t.SourceTemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.ConfigureAuthorTracking(t => t.Creator, t => t.Updater);
        builder.ConfigureSoftDelete();

        builder.HasMany(t => t.Fields)
            .WithOne(f => f.CustomTable)
            .HasForeignKey(f => f.CustomTableId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Records)
            .WithOne(r => r.CustomTable)
            .HasForeignKey(r => r.CustomTableId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
