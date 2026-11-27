using Crm.Domain.Entities;

namespace Crm.Application.Interfaces;

/// <summary>
/// Manages manual row positioning for <see cref="CustomRecord"/> entities using lexicographical fractional indexing,
/// supporting tail/head/middle insertion, drag-and-drop moves, optimistic adjacency conflict checks, and automatic rebalancing.
/// </summary>
public interface ICustomRecordPositionService
{
    /// <summary>
    /// Validates optional neighbor placement and computes a fractional <see cref="CustomRecord.Position"/> rank
    /// for a new record, automatically rebalancing the table if the computed rank exceeds the threshold.
    /// The caller must hold the parent custom table write lock within an active transaction.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the parent custom table.</param>
    /// <param name="previousRecordId">Optional identifier of the preceding neighbor; <see langword="null"/> when inserting at head or appending to tail.</param>
    /// <param name="nextRecordId">Optional identifier of the following neighbor; <see langword="null"/> when appending to tail.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The computed fractional-index rank for the new record.</returns>
    Task<string> ComputeInsertPositionAsync(
        Guid customTableId,
        Guid? previousRecordId,
        Guid? nextRecordId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires the parent custom table write lock and repositions an existing custom record to a new slot
    /// between <paramref name="previousRecordId"/> and <paramref name="nextRecordId"/> (drag-and-drop)
    /// by updating only the moved record's <see cref="CustomRecord.Position"/>.
    /// </summary>
    /// <param name="recordId">The unique identifier of the custom record to move.</param>
    /// <param name="previousRecordId">Optional identifier of the new preceding neighbor.</param>
    /// <param name="nextRecordId">Optional identifier of the new following neighbor.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MoveAsync(
        Guid recordId,
        Guid? previousRecordId,
        Guid? nextRecordId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebalances all <see cref="CustomRecord.Position"/> ranks in the specified table to compact evenly spaced keys
    /// while strictly preserving their current manual order.
    /// The caller must hold the parent custom table write lock within an active transaction.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the owning custom table.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous rebalance operation.</returns>
    Task RebalanceTableAsync(
        Guid customTableId,
        CancellationToken cancellationToken = default);
}
