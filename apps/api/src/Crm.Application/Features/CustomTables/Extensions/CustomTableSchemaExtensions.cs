using Crm.Application.Extensions;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Application.Features.CustomTables.Extensions;

/// <summary>
/// Contains extension methods for calculating structural schema differences on <see cref="CustomTable"/> entities.
/// </summary>
public static class CustomTableSchemaExtensions
{
    /// <summary>
    /// Computes structural schema differences between the current table field state and an incoming set of field update DTOs,
    /// synchronizing the table's <see cref="CustomTable.Fields"/> collection in-place by delegating to
    /// <see cref="FieldSchemaSynchronizer.ComputeSchemaDiff{TEntity, TDto}"/>.
    /// </summary>
    /// <typeparam name="TDto">The incoming field DTO type implementing <see cref="IIdentifiableField"/>.</typeparam>
    /// <param name="table">The target custom table entity containing the current field schema.</param>
    /// <param name="incomingFields">The collection of updated field definitions from the request payload.</param>
    /// <param name="hasExistingRecords">Indicates whether the custom table contains existing records in the database.</param>
    /// <returns>A <see cref="SchemaDiffResult"/> instance describing all required field changes, key renames, and record backfill requirements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="table"/> or <paramref name="incomingFields"/> is <see langword="null"/>.</exception>
    /// <exception cref="EntityNotFoundException{CustomField}">Thrown when an incoming field ID does not exist within the table's current field collection.</exception>
    /// <exception cref="FieldDataTypeChangeNotAllowedException">Thrown when attempting to modify a field's data type on a table that already contains records.</exception>
    /// <exception cref="DomainValidationException">Thrown when duplicate field IDs are present within incoming fields.</exception>
    public static SchemaDiffResult ComputeSchemaDiff<TDto>(
        this CustomTable table,
        IReadOnlyCollection<TDto> incomingFields,
        bool hasExistingRecords)
        where TDto : IIdentifiableField
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(incomingFields);

        return table.Fields.ComputeSchemaDiff(incomingFields, _ => hasExistingRecords);
    }
}
