using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides shared domain validation and reordering logic for collections of <see cref="IOrderedEntity"/>
/// that must maintain a contiguous 0-based <c>Order</c> sequence (<c>0, 1, ..., count - 1</c>) without gaps or duplicates.
/// </summary>
public static class SequentialOrderExtensions
{
    /// <summary>
    /// Validates that <paramref name="items"/> form a continuous, duplicate-free 0-based sequence (<c>0 .. count - 1</c>).
    /// </summary>
    /// <typeparam name="T">The ordered item type implementing <see cref="IOrderedEntity"/>.</typeparam>
    /// <param name="items">The collection of ordered items to validate.</param>
    /// <param name="onNonSequential">Factory producing the exception thrown when any order value is out of range or leaves a gap.</param>
    /// <param name="onDuplicate">Factory producing the exception thrown when duplicate order values are detected.</param>
    public static void EnsureSequentialOrders<T>(
        this IReadOnlyCollection<T> items,
        Func<Exception>? onNonSequential = null,
        Func<Exception>? onDuplicate = null)
        where T : IOrderedEntity
    {
        ArgumentNullException.ThrowIfNull(items);

        int count = items.Count;
        if (count == 0)
        {
            return;
        }

        var seenOrders = new bool[count];
        foreach (T item in items)
        {
            byte order = item.Order;
            if (order >= count)
            {
                throw onNonSequential?.Invoke() ?? new NonSequentialOrderException();
            }

            if (seenOrders[order])
            {
                throw onDuplicate?.Invoke() ?? new DomainValidationException($"Duplicate display order value '{order}' is not allowed.");
            }

            seenOrders[order] = true;
        }
    }

    /// <summary>
    /// Validates that adding <paramref name="newOrder"/> to <paramref name="existingOrders"/> neither duplicates an
    /// existing order value nor introduces a gap in the 0-based sequence (<c>0 .. existingOrders.Count</c>).
    /// </summary>
    /// <param name="existingOrders">The existing order values already persisted in the parent scope.</param>
    /// <param name="newOrder">The candidate order value for the new item.</param>
    /// <param name="onDuplicate">Factory producing the exception thrown when <paramref name="newOrder"/> is already taken.</param>
    /// <param name="onNonSequential">Factory producing the exception thrown when <paramref name="newOrder"/> skips ahead of the next sequential position.</param>
    public static void EnsureValidNextOrder(
        this IReadOnlyCollection<byte> existingOrders,
        byte newOrder,
        Func<Exception> onDuplicate,
        Func<Exception>? onNonSequential = null)
    {
        ArgumentNullException.ThrowIfNull(existingOrders);
        ArgumentNullException.ThrowIfNull(onDuplicate);

        int nextCount = existingOrders.Count + 1;
        if (nextCount > byte.MaxValue + 1)
        {
            throw new DomainValidationException($"Cannot exceed the maximum sequential display order limit of {byte.MaxValue + 1} items.");
        }

        if (existingOrders.Contains(newOrder))
        {
            throw onDuplicate();
        }

        var seenOrders = new bool[nextCount];
        foreach (byte existing in existingOrders)
        {
            if (existing < nextCount)
            {
                seenOrders[existing] = true;
            }
        }

        if (newOrder >= nextCount || seenOrders[newOrder])
        {
            throw onNonSequential?.Invoke() ?? new NonSequentialOrderException();
        }

        seenOrders[newOrder] = true;

        for (int i = 0; i < nextCount; i++)
        {
            if (!seenOrders[i])
            {
                throw onNonSequential?.Invoke() ?? new NonSequentialOrderException();
            }
        }
    }

    /// <summary>
    /// Moves <paramref name="itemToMove"/> to <paramref name="targetOrder"/> within <paramref name="items"/>
    /// and renumbers all items contiguously (<c>0 .. count - 1</c>), throwing if <paramref name="targetOrder"/>
    /// is outside the contiguous range <c>0 .. count - 1</c>.
    /// </summary>
    /// <typeparam name="T">The ordered entity type.</typeparam>
    /// <param name="items">All items belonging to the same parent scope.</param>
    /// <param name="itemToMove">The item whose display order is being updated.</param>
    /// <param name="targetOrder">The desired 0-based target order.</param>
    /// <param name="setOrder">Setter delegate applying the recalculated 0-based order to each item.</param>
    /// <param name="onNonSequential">Optional factory producing the exception thrown when <paramref name="targetOrder"/> is out of bounds.</param>
    public static void ReorderSequential<T>(
        this IReadOnlyCollection<T> items,
        T itemToMove,
        byte targetOrder,
        Action<T, byte> setOrder,
        Func<Exception>? onNonSequential = null)
        where T : class, IOrderedEntity
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(itemToMove);
        ArgumentNullException.ThrowIfNull(setOrder);

        if (targetOrder >= items.Count)
        {
            throw onNonSequential?.Invoke() ?? new NonSequentialOrderException();
        }

        List<T> ordered = [.. items.OrderBy(i => i.Order)];
        int currentIndex = ordered.IndexOf(itemToMove);
        if (currentIndex < 0)
        {
            throw new ArgumentException("Item to reorder must be present in the collection.", nameof(itemToMove));
        }

        ordered.RemoveAt(currentIndex);
        ordered.Insert(targetOrder, itemToMove);

        for (int i = 0; i < ordered.Count; i++)
        {
            byte expectedOrder = (byte)i;
            if (ordered[i].Order != expectedOrder)
            {
                setOrder(ordered[i], expectedOrder);
            }
        }
    }

    /// <summary>
    /// Compacts the remaining items after a deletion so their display orders remain contiguous (<c>0 .. count - 1</c>).
    /// </summary>
    /// <typeparam name="T">The ordered entity type.</typeparam>
    /// <param name="remainingItems">The remaining items in the parent scope after removing an item.</param>
    /// <param name="setOrder">Setter delegate applying the compacted 0-based order to each item.</param>
    public static void CompactSequential<T>(
        this IReadOnlyCollection<T> remainingItems,
        Action<T, byte> setOrder)
        where T : IOrderedEntity
    {
        ArgumentNullException.ThrowIfNull(remainingItems);
        ArgumentNullException.ThrowIfNull(setOrder);

        List<T> ordered = [.. remainingItems.OrderBy(i => i.Order)];
        for (int i = 0; i < ordered.Count; i++)
        {
            byte expectedOrder = (byte)i;
            if (ordered[i].Order != expectedOrder)
            {
                setOrder(ordered[i], expectedOrder);
            }
        }
    }
}
