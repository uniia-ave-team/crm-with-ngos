using System.Text.Json.Nodes;
using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities.Base;
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
    /// Configures common schema-field columns (<see cref="FieldDefinitionBase.Name"/>,
    /// <see cref="FieldDefinitionBase.IsRequired"/>, <see cref="FieldDefinitionBase.Order"/>)
    /// shared by custom table fields and global user custom fields.
    /// </summary>
    /// <typeparam name="TField">The field entity type inheriting <see cref="FieldDefinitionBase"/>.</typeparam>
    /// <param name="builder">The EF Core entity type builder.</param>
    public static void ConfigureFieldDefinition<TField>(
        this EntityTypeBuilder<TField> builder)
        where TField : FieldDefinitionBase
    {
        builder.ConfigureGuidV7Key();

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(FieldSchemaValidationConstants.MaxNameLength);

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
