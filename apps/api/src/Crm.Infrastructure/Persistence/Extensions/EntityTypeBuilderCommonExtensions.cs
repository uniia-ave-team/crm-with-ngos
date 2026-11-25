using System.Text.Json.Nodes;
using Crm.Domain.Constants.Validation;
using Crm.Domain.Interfaces.Entities;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.ValueComparers;
using Crm.Infrastructure.Persistence.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Provides reusable EF Core <see cref="EntityTypeBuilder{TEntity}"/> and <see cref="PropertyBuilder{TProperty}"/>
/// configuration helpers to eliminate duplication across entity type configurations.
/// </summary>
internal static class EntityTypeBuilderCommonExtensions
{
    /// <summary>
    /// Configures <see cref="IEntity.Id"/> as the primary key backed by <see cref="GuidV7ValueGenerator"/>.
    /// </summary>
    /// <typeparam name="TEntity">The configured entity type.</typeparam>
    /// <param name="builder">The EF Core entity type builder.</param>
    public static void ConfigureGuidV7Key<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IEntity
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasValueGenerator<GuidV7ValueGenerator>();
    }

    /// <summary>
    /// Configures common schema-field columns (<see cref="IFieldSchema.Name"/>, <see cref="IFieldSchema.Key"/>,
    /// <see cref="IFieldSchema.DataType"/>, <see cref="IFieldSchema.IsRequired"/>, <see cref="IOrderedEntity.Order"/>)
    /// shared by custom table fields, template fields, and user custom fields.
    /// </summary>
    /// <typeparam name="TField">The field entity type implementing <see cref="IEntityField"/>.</typeparam>
    /// <param name="builder">The EF Core entity type builder.</param>
    /// <param name="storeDataTypeAsString">
    /// Whether to persist <see cref="IFieldSchema.DataType"/> as a string column (<see langword="true"/> for custom tables/templates)
    /// or as its underlying integer value (<see langword="false"/> for user custom fields).
    /// </param>
    public static void ConfigureFieldDefinition<TField>(
        this EntityTypeBuilder<TField> builder,
        bool storeDataTypeAsString = true)
        where TField : class, IEntityField
    {
        builder.ConfigureGuidV7Key();

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(FieldSchemaValidationConstants.MaxNameLength);

        builder.Property(f => f.Key)
            .IsRequired()
            .HasMaxLength(FieldSchemaValidationConstants.MaxKeyLength);

        if (storeDataTypeAsString)
        {
            builder.Property(f => f.DataType)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(FieldSchemaValidationConstants.MaxDataTypeLength);
        }
        else
        {
            builder.Property(f => f.DataType)
                .IsRequired();
        }

        builder.Property(f => f.IsRequired)
            .IsRequired();

        builder.Property(f => f.Order)
            .IsRequired();
    }

    /// <summary>
    /// Configures a <see cref="JsonObject"/> property as a required PostgreSQL <c>jsonb</c> column
    /// equipped with <see cref="JsonObjectValueComparer"/> for accurate in-place change tracking.
    /// </summary>
    /// <param name="builder">The property builder for the <see cref="JsonObject"/> column.</param>
    /// <returns>The configured property builder for chaining.</returns>
    public static PropertyBuilder<JsonObject> ConfigureJsonbColumn(this PropertyBuilder<JsonObject> builder)
    {
        builder.HasColumnType(PostgresConstants.Jsonb)
            .IsRequired();

        builder.Metadata.SetValueComparer(JsonObjectValueComparer.Instance);

        return builder;
    }
}
