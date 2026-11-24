using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace Crm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="CustomRecord"/> entity.
/// </summary>
public sealed class CustomRecordConfiguration : IEntityTypeConfiguration<CustomRecord>
{
    private static readonly string ComputedSearchVectorSql =
        $"jsonb_to_tsvector('{CustomRecordSearchConstants.SearchVectorConfiguration}', \"Data\", '[\"string\",\"numeric\"]')";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CustomRecord> builder)
    {
        builder.ConfigureGuidV7Key();

        builder.Property(r => r.Data)
            .ConfigureJsonbColumn();

        builder.Property<NpgsqlTsVector>(CustomRecordSearchConstants.SearchVectorPropertyName)
            .HasComputedColumnSql(ComputedSearchVectorSql, stored: true);

        builder.HasIndex(CustomRecordSearchConstants.SearchVectorPropertyName)
            .HasMethod(PostgresConstants.Gin);

        builder.ConfigureAuthorTracking(r => r.Creator, r => r.Updater);
        builder.HasQueryFilter(r => !r.CustomTable.IsDeleted);

        builder.HasIndex(r => new { r.CustomTableId, r.Id });
    }
}
