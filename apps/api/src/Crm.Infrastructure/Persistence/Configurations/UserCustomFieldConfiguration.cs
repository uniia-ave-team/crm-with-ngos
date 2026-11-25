using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the global <see cref="UserCustomField"/> schema entity.
/// </summary>
public sealed class UserCustomFieldConfiguration : IEntityTypeConfiguration<UserCustomField>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserCustomField> builder)
    {
        builder.ConfigureFieldDefinition(storeDataTypeAsString: false);

        builder.Property(cf => cf.IsPublic)
            .IsRequired();

        builder.HasIndex(cf => cf.Key)
            .IsUnique();

        builder.HasIndex(cf => cf.Order);
    }
}
