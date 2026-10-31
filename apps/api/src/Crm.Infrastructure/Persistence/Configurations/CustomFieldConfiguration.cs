using Crm.Domain.Consts.Entities.ValidationConstants;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the entity framework mapping for the <see cref="CustomField"/> entity.
/// </summary>
public class CustomFieldConfiguration : IEntityTypeConfiguration<CustomField>
{
    public void Configure(EntityTypeBuilder<CustomField> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(CustomFieldValidationConstants.MaxNameLength);

        builder.Property(f => f.Key)
            .IsRequired()
            .HasMaxLength(CustomFieldValidationConstants.MaxKeyLength);

        builder.Property(f => f.DataType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(CustomFieldValidationConstants.MaxDataTypeLength);

        builder.Property(f => f.IsRequired)
            .IsRequired();

        builder.Property(f => f.Order)
            .IsRequired();

        builder.HasIndex(f => new { f.CustomTableId, f.Key })
            .IsUnique();
    }
}
