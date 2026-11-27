using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;

namespace Crm.Application.Features.CustomRecords.Services;

/// <summary>
/// Implements manual row positioning (insert, drag-and-drop move, and rebalancing) for <see cref="CustomRecord"/> entities
/// using <see cref="FractionalIndex"/> ranks, pessimistic table locks, optimistic neighbor adjacency validation,
/// and automatic rank rebalancing when rank length exceeds <see cref="FractionalIndex.RebalanceThresholdLength"/>.
/// </summary>
/// <param name="customTableRepository">Repository providing row-level table locks.</param>
/// <param name="customRecordRepository">Repository for reading and updating record positions.</param>
public sealed class CustomRecordPositionService(
    ICustomTableRepository customTableRepository,
    ICustomRecordRepository customRecordRepository) : ICustomRecordPositionService
{
    /// <inheritdoc />
    public async Task<string> ComputeInsertPositionAsync(
        Guid customTableId,
        Guid? previousRecordId,
        Guid? nextRecordId,
        CancellationToken cancellationToken = default)
    {
        ValidateNeighborIds(previousRecordId, nextRecordId, movingRecordId: null);

        (string position, _) = await ComputeTargetPositionAsync(
            customTableId,
            movingRecord: null,
            previousRecordId,
            nextRecordId,
            cancellationToken);

        return position;
    }

    /// <inheritdoc />
    public async Task MoveAsync(
        Guid recordId,
        Guid? previousRecordId,
        Guid? nextRecordId,
        CancellationToken cancellationToken = default)
    {
        ValidateNeighborIds(previousRecordId, nextRecordId, movingRecordId: recordId);

        var initial = await customRecordRepository.GetPositionInfoAsync(recordId, cancellationToken)
            ?? throw new EntityNotFoundException<CustomRecord>(recordId);

        await customTableRepository.LockForUpdateAsync(initial.CustomTableId, cancellationToken);

        var record = await customRecordRepository.GetPositionInfoAsync(recordId, cancellationToken)
            ?? throw new EntityNotFoundException<CustomRecord>(recordId);

        if (previousRecordId is null && nextRecordId is null)
        {
            bool hasAfter = await customRecordRepository.HasRecordInPositionRangeAsync(
                record.CustomTableId,
                exclusiveLowerBound: record.Position,
                excludeRecordId: record.Id,
                cancellationToken: cancellationToken);

            if (!hasAfter)
            {
                return;
            }
        }

        (string newPosition, bool isNoOp) = await ComputeTargetPositionAsync(
            record.CustomTableId,
            movingRecord: record,
            previousRecordId,
            nextRecordId,
            cancellationToken);

        if (isNoOp)
        {
            return;
        }

        await customRecordRepository.UpdatePositionAsync(record.Id, newPosition, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RebalanceTableAsync(Guid customTableId, CancellationToken cancellationToken = default)
    {
        var orderedIds = await customRecordRepository.GetOrderedIdsAsync(customTableId, cancellationToken);
        if (orderedIds.Count == 0)
        {
            return;
        }

        var compactRanks = FractionalIndex.GenerateEvenlySpaced(orderedIds.Count);
        var positionsById = new Dictionary<Guid, string>(orderedIds.Count);

        for (int i = 0; i < orderedIds.Count; i++)
        {
            positionsById[orderedIds[i]] = compactRanks[i];
        }

        await customRecordRepository.ReplaceAllPositionsAsync(customTableId, positionsById, cancellationToken);
    }

    private async Task<(string Position, bool IsNoOp)> ComputeTargetPositionAsync(
        Guid customTableId,
        CustomRecordPositionInfo? movingRecord,
        Guid? previousRecordId,
        Guid? nextRecordId,
        CancellationToken cancellationToken)
    {
        var result = await ResolveAndComputeRankAsync(
            customTableId,
            movingRecord,
            previousRecordId,
            nextRecordId,
            cancellationToken);

        if (result.IsNoOp || result.Position.Length <= FractionalIndex.RebalanceThresholdLength)
        {
            return result;
        }

        await RebalanceTableAsync(customTableId, cancellationToken);

        CustomRecordPositionInfo? refreshedMoving = movingRecord is null
            ? null
            : await customRecordRepository.GetPositionInfoAsync(movingRecord.Id, cancellationToken);

        return await ResolveAndComputeRankAsync(
            customTableId,
            refreshedMoving,
            previousRecordId,
            nextRecordId,
            cancellationToken);
    }

    private async Task<(string Position, bool IsNoOp)> ResolveAndComputeRankAsync(
        Guid customTableId,
        CustomRecordPositionInfo? movingRecord,
        Guid? previousRecordId,
        Guid? nextRecordId,
        CancellationToken cancellationToken)
    {
        if (previousRecordId is null && nextRecordId is null)
        {
            string? lastPosition = await customRecordRepository.GetLastPositionAsync(customTableId, cancellationToken);
            return (FractionalIndex.Between(lastPosition, null), false);
        }

        var (prev, next) = await ResolveNeighborsAsync(customTableId, previousRecordId, nextRecordId, cancellationToken);

        await EnsureNeighborsAdjacentAsync(customTableId, prev, next, movingRecord?.Id, cancellationToken);

        return movingRecord is not null && IsAlreadyInTargetSlot(movingRecord.Position, prev?.Position, next?.Position)
            ? (movingRecord.Position, true)
            : (FractionalIndex.Between(prev?.Position, next?.Position), false);
    }

    private async Task<(CustomRecordPositionInfo? Prev, CustomRecordPositionInfo? Next)> ResolveNeighborsAsync(
        Guid customTableId,
        Guid? previousRecordId,
        Guid? nextRecordId,
        CancellationToken cancellationToken)
    {
        List<Guid> requestedIds = new(2);
        if (previousRecordId is { } prevId)
        {
            requestedIds.Add(prevId);
        }

        if (nextRecordId is { } nextId)
        {
            requestedIds.Add(nextId);
        }

        var neighbors = await customRecordRepository.GetPositionInfosByIdsAsync(
            customTableId,
            requestedIds,
            cancellationToken);

        return (
            ResolveNeighbor(neighbors, previousRecordId),
            ResolveNeighbor(neighbors, nextRecordId));
    }

    private static CustomRecordPositionInfo? ResolveNeighbor(
        IReadOnlyDictionary<Guid, CustomRecordPositionInfo> neighbors,
        Guid? neighborId)
        => neighborId is not { } expectedId
            ? null
            : neighbors.TryGetValue(expectedId, out var info)
                ? info
                : throw new EntityNotFoundException<CustomRecord>(expectedId);

    private async Task EnsureNeighborsAdjacentAsync(
        Guid customTableId,
        CustomRecordPositionInfo? prev,
        CustomRecordPositionInfo? next,
        Guid? movingRecordId,
        CancellationToken cancellationToken)
    {
        bool invertedBounds = prev is not null
            && next is not null
            && string.CompareOrdinal(prev.Position, next.Position) >= 0;

        if (!invertedBounds
            && !await customRecordRepository.HasRecordInPositionRangeAsync(
                customTableId,
                exclusiveLowerBound: prev?.Position,
                exclusiveUpperBound: next?.Position,
                excludeRecordId: movingRecordId,
                cancellationToken: cancellationToken))
        {
            return;
        }

        string message = (prev, next) switch
        {
            (not null, not null) => "The specified neighboring records are no longer adjacent. Please refresh and retry.",
            (not null, null) => "The specified previous record is no longer the last record in the table. Please refresh and retry.",
            _ => "The specified next record is no longer the first record in the table. Please refresh and retry.",
        };

        throw new ConcurrencyConflictException(message);
    }

    private static bool IsAlreadyInTargetSlot(string currentPosition, string? prevPosition, string? nextPosition)
        => (prevPosition is null || string.CompareOrdinal(prevPosition, currentPosition) < 0)
            && (nextPosition is null || string.CompareOrdinal(currentPosition, nextPosition) < 0);

    private static void ValidateNeighborIds(Guid? previousRecordId, Guid? nextRecordId, Guid? movingRecordId)
    {
        if (previousRecordId == Guid.Empty)
        {
            throw new DomainValidationException("Previous record ID cannot be an empty GUID.");
        }

        if (nextRecordId == Guid.Empty)
        {
            throw new DomainValidationException("Next record ID cannot be an empty GUID.");
        }

        if (previousRecordId is not null && previousRecordId == nextRecordId)
        {
            throw new DomainValidationException("Previous and next record identifiers cannot be the same.");
        }

        if (movingRecordId is { } selfId && (previousRecordId == selfId || nextRecordId == selfId))
        {
            throw new DomainValidationException("A record cannot reference itself as a neighboring record.");
        }
    }
}
