using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the entity framework mapping for the <see cref="CustomRecord"/> entity.
/// </summary>
public class CustomRecordConfiguration : IEntityTypeConfiguration<CustomRecord>
{
    public void Configure(EntityTypeBuilder<CustomRecord> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();

        builder.Property(r => r.Data)
            .HasColumnType(nameof(NpgsqlDbType.Jsonb).ToLowerInvariant())
            .IsRequired();
    }
}
