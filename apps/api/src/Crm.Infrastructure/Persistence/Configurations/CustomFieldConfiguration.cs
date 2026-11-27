using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="CustomField"/> entity.
/// </summary>
public sealed class CustomFieldConfiguration : IEntityTypeConfiguration<CustomField>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CustomField> builder)
    {
        builder.ConfigureFieldDefinition();

        builder.HasIndex(f => new { f.CustomTableId, f.Order });

        builder.HasQueryFilter(f => !f.CustomTable.IsDeleted);
    }
}
