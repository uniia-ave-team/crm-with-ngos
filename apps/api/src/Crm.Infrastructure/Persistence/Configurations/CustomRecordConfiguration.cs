using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="CustomRecord"/> entity,
/// including its unique fractional-indexing <see cref="CustomRecord.Position"/> index and JSONB full-text search vector.
/// </summary>
public sealed class CustomRecordConfiguration : IEntityTypeConfiguration<CustomRecord>
{
    private static readonly string ComputedSearchVectorSql =
        $"jsonb_to_tsvector('{CustomRecordSearchConstants.SearchVectorConfiguration}', \"{nameof(CustomRecord.Data)}\", '[\"string\"]')";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CustomRecord> builder)
    {
        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_CustomRecord_Position_NotEmpty",
                $"\"{nameof(CustomRecord.Position)}\" <> ''"));

        builder.ConfigureGuidV7Key();

        builder.Property(r => r.Position)
            .IsRequired()
            .HasMaxLength(FractionalIndex.MaxColumnLength)
            .UseCollation(PostgresConstants.CCollation);

        builder.HasIndex(r => new { r.CustomTableId, r.Position })
            .IsUnique();

        builder.Property(r => r.Data)
            .ConfigureJsonbColumn()
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property<NpgsqlTsVector>(CustomRecordSearchConstants.SearchVectorPropertyName)
            .HasComputedColumnSql(ComputedSearchVectorSql, stored: true);

        builder.HasIndex(CustomRecordSearchConstants.SearchVectorPropertyName)
            .HasMethod(PostgresConstants.Gin);

        builder.ConfigureAuthorTracking(r => r.Creator, r => r.Updater);
        builder.HasQueryFilter(r => !r.CustomTable.IsDeleted);
    }
}
