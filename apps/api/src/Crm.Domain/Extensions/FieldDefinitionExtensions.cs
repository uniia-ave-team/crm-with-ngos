using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Extensions;

/// <summary>
/// Contains extension methods for validating and normalizing collections of <see cref="IFieldSchema"/>.
/// </summary>
public static class FieldDefinitionExtensions
{
    /// <summary>
    /// Validates that the field collection contains unique display orders starting from 0 and unique keys,
    /// optionally allowing an empty schema collection.
    /// </summary>
    /// <typeparam name="T">The type of entity implementing <see cref="IFieldSchema"/>.</typeparam>
    /// <param name="entities">The collection of ordered entities to validate.</param>
    /// <param name="allowEmpty">When <see langword="true"/>, an empty collection is considered valid.</param>
    /// <exception cref="EmptyFieldSchemaException">Thrown when <paramref name="allowEmpty"/> is <see langword="false"/> and the collection is null or empty.</exception>
    /// <exception cref="DuplicateFieldOrderException">Thrown when duplicate display order values are found.</exception>
    /// <exception cref="NonSequentialFieldOrderException">Thrown when order numbers are not sequential starting from zero.</exception>
    /// <exception cref="DuplicateFieldKeyException">Thrown when duplicate key values are found.</exception>
    public static void EnsureValidFieldSchema<T>(this IEnumerable<T>? entities, bool allowEmpty = false)
        where T : IFieldSchema
    {
        if (entities is null)
        {
            if (allowEmpty)
            {
                return;
            }

            throw new EmptyFieldSchemaException();
        }

        var list = entities as IReadOnlyCollection<T> ?? [.. entities];
        var count = list.Count;

        if (count == 0)
        {
            if (allowEmpty)
            {
                return;
            }

            throw new EmptyFieldSchemaException();
        }

        var keys = new HashSet<string>(count, StringComparer.Ordinal);
        foreach (var field in list)
        {
            if (!keys.Add(field.Key))
            {
                throw new DuplicateFieldKeyException();
            }
        }

        list.EnsureSequentialOrders(
            onNonSequential: static () => new NonSequentialFieldOrderException(),
            onDuplicate: static () => new DuplicateFieldOrderException());
    }
}
