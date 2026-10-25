using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces;

namespace Crm.Domain.Extensions;

/// <summary>
/// Contains extension methods for validating and normalizing collections of <see cref="IFieldSchema"/>.
/// </summary>
public static class FieldDefinitionExtensions
{
    /// <summary>
    /// Validates that the field collection is non-empty, contains unique display orders starting from 0, and has unique keys.
    /// </summary>
    /// <typeparam name="T">The type of entity implementing <see cref="IFieldSchema"/>.</typeparam>
    /// <param name="entities">The collection of ordered entities to validate.</param>
    /// <exception cref="EmptyFieldSchemaException">Thrown when the collection is null or contains no elements.</exception>
    /// <exception cref="DuplicateFieldOrderException">Thrown when duplicate display order values are found.</exception>
    /// <exception cref="NonSequentialFieldOrderException">Thrown when order numbers are not sequential starting from zero.</exception>
    /// <exception cref="DuplicateFieldKeyException">Thrown when duplicate key values are found.</exception>
    public static void EnsureValidFieldSchema<T>(this IEnumerable<T>? entities)
        where T : IFieldSchema
    {
        if (entities is null)
        {
            throw new EmptyFieldSchemaException();
        }

        var list = entities as IReadOnlyCollection<T> ?? [.. entities];
        var count = list.Count;

        if (count == 0)
        {
            throw new EmptyFieldSchemaException();
        }

        var keys = new HashSet<string>(count, StringComparer.Ordinal);
        var seenOrders = new bool[count];

        foreach (var field in list)
        {
            if (!keys.Add(field.Key))
            {
                throw new DuplicateFieldKeyException();
            }

            var order = field.Order;
            if (order >= count)
            {
                throw new NonSequentialFieldOrderException();
            }

            if (seenOrders[order])
            {
                throw new DuplicateFieldOrderException();
            }

            seenOrders[order] = true;
        }
    }
}
