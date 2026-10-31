using Crm.Domain.Consts.Entities.ValidationConstants;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the entity framework mapping for the <see cref="TableTemplate"/> entity.
/// </summary>
public class TableTemplateConfiguration : IEntityTypeConfiguration<TableTemplate>
{
    public void Configure(EntityTypeBuilder<TableTemplate> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(TableTemplateValidationConstants.MaxNameLength);

        builder.Property(t => t.Description)
            .HasMaxLength(TableTemplateValidationConstants.MaxDescriptionLength);

        builder.Property(t => t.IsSystemPreset)
            .IsRequired();

        builder.HasMany(t => t.Fields)
            .WithOne(f => f.Template)
            .HasForeignKey(f => f.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
