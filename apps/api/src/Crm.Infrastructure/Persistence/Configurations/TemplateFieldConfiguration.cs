using Crm.Domain.Consts.Entities;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the entity framework mapping for the <see cref="TemplateField"/> entity.
/// </summary>
public class TemplateFieldConfiguration : IEntityTypeConfiguration<TemplateField>
{
    public void Configure(EntityTypeBuilder<TemplateField> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(TemplateFieldValidationConstants.MaxNameLength);

        builder.Property(f => f.Key)
            .IsRequired()
            .HasMaxLength(TemplateFieldValidationConstants.MaxKeyLength);

        builder.Property(f => f.DataType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(TemplateFieldValidationConstants.MaxDataTypeLength);

        builder.Property(f => f.IsRequired)
            .IsRequired();

        builder.Property(f => f.Order)
            .IsRequired();

        builder.HasIndex(f => new { f.TemplateId, f.Key })
            .IsUnique();

        builder.HasIndex(f => new { f.TemplateId, f.Order });
    }
}
