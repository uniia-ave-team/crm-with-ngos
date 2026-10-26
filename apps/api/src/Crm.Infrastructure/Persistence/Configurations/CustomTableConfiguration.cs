using Crm.Domain.Consts.Entities;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the entity framework mapping for the <see cref="CustomTable"/> entity.
/// </summary>
public class CustomTableConfiguration : IEntityTypeConfiguration<CustomTable>
{
    public void Configure(EntityTypeBuilder<CustomTable> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

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
