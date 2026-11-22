using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="TemplateField"/> entity.
/// </summary>
public sealed class TemplateFieldConfiguration : IEntityTypeConfiguration<TemplateField>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TemplateField> builder)
    {
        builder.ConfigureFieldDefinition();

        builder.HasIndex(f => new { f.TemplateId, f.Key })
            .IsUnique();

        builder.HasIndex(f => new { f.TemplateId, f.Order });
    }
}
