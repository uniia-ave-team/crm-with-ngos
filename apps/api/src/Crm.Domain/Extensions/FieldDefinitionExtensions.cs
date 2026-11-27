using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Extensions;

/// <summary>
/// Contains extension methods for validating collections of <see cref="IFieldDefinition"/>.
/// </summary>
public static class FieldDefinitionExtensions
{
    /// <summary>
    /// Validates that the field collection contains unique display orders starting from 0,
    /// optionally allowing an empty schema collection.
    /// </summary>
    /// <typeparam name="T">The type implementing <see cref="IFieldDefinition"/>.</typeparam>
    /// <param name="entities">The collection of ordered field definitions to validate.</param>
    /// <param name="allowEmpty">When <see langword="true"/>, an empty collection is considered valid.</param>
    /// <exception cref="DomainValidationException">Thrown when <paramref name="allowEmpty"/> is <see langword="false"/> and the collection is null or empty.</exception>
    /// <exception cref="DuplicateFieldOrderException">Thrown when duplicate display order values are found.</exception>
    /// <exception cref="NonSequentialFieldOrderException">Thrown when order numbers are not sequential starting from zero.</exception>
    public static void EnsureValidFieldSchema<T>(this IEnumerable<T>? entities, bool allowEmpty = false)
        where T : IFieldDefinition
    {
        var list = entities as IReadOnlyCollection<T> ?? (entities is null ? [] : [.. entities]);

        if (list.Count == 0)
        {
            if (allowEmpty)
            {
                return;
            }

            throw new DomainValidationException("A field schema must contain at least one field.");
        }

        list.EnsureSequentialOrders(
            onNonSequential: static () => new NonSequentialFieldOrderException(),
            onDuplicate: static () => new DuplicateFieldOrderException());
    }
}
