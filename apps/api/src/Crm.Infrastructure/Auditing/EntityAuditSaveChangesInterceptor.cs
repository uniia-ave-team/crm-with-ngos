using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Entities;
using Crm.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Auditing;

/// <summary>
/// Entity Framework Core <see cref="SaveChangesInterceptor"/> and <see cref="IDbTransactionInterceptor"/> that
/// automatically captures before/after diffs for every tracked entity decorated with
/// <see cref="Domain.Attributes.AuditableAttribute"/> and dispatches them asynchronously to <see cref="IAuditLogBuffer"/>
/// only after persistence (and any enclosing database transaction) commits successfully.
/// </summary>
/// <param name="auditLogBuffer">The non-blocking background audit buffer.</param>
/// <param name="actorAccessor">Accessor resolving the current user and correlation trace ID.</param>
/// <param name="options">Auditing feature configuration options.</param>
/// <param name="timeProvider">Time provider used to stamp UTC audit timestamps.</param>
public sealed class EntityAuditSaveChangesInterceptor(
    IAuditLogBuffer auditLogBuffer,
    IAuditActorAccessor actorAccessor,
    IOptions<AuditOptions> options,
    TimeProvider timeProvider) : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private const string RedactedPlaceholder = "[REDACTED]";

    private static readonly IReadOnlyDictionary<string, AuditPropertyChange> EmptyChanges =
        new Dictionary<string, AuditPropertyChange>(StringComparer.Ordinal);

    private static readonly JsonSerializerOptions FallbackSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ConditionalWeakTable<DbContext, List<PendingAuditEntry>> _pendingEntries = [];
    private readonly ConditionalWeakTable<DbTransaction, List<EntityAuditRecord>> _transactionRecords = [];

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CapturePendingEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CapturePendingEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushPendingEntries(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        FlushPendingEntries(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        DiscardPendingEntries(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    /// <inheritdoc />
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        DiscardPendingEntries(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    /// <inheritdoc />
    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        => FlushTransactionRecords(transaction);

    /// <inheritdoc />
    public Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        FlushTransactionRecords(transaction);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        => DiscardTransactionRecords(transaction);

    /// <inheritdoc />
    public Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        DiscardTransactionRecords(transaction);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
        => DiscardTransactionRecords(transaction);

    /// <inheritdoc />
    public Task TransactionFailedAsync(
        DbTransaction transaction,
        TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        DiscardTransactionRecords(transaction);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Records an audit entry for a direct bulk SQL deletion (<c>ExecuteDeleteAsync</c>) when
    /// <typeparamref name="TEntity"/> is decorated with <see cref="Domain.Attributes.AuditableAttribute"/>.
    /// </summary>
    /// <typeparam name="TEntity">The deleted entity type.</typeparam>
    /// <param name="context">The active database context executing the bulk operation.</param>
    /// <param name="entityKey">The stringified primary key of the deleted entity.</param>
    internal void RecordBulkDelete<TEntity>(DbContext context, string entityKey)
        where TEntity : class
        => RecordBulkAudit<TEntity>(context, entityKey, AuditAction.Deleted, EmptyChanges);

    /// <summary>
    /// Records an audit entry for a direct bulk SQL update (<c>ExecuteUpdateAsync</c>) when
    /// <typeparamref name="TEntity"/> is decorated with <see cref="Domain.Attributes.AuditableAttribute"/>.
    /// </summary>
    /// <typeparam name="TEntity">The updated entity type.</typeparam>
    /// <param name="context">The active database context executing the bulk operation.</param>
    /// <param name="entityKey">The stringified primary key of the updated entity.</param>
    /// <param name="changes">The property-level before/after diffs applied by the bulk update.</param>
    internal void RecordBulkUpdate<TEntity>(
        DbContext context,
        string entityKey,
        IReadOnlyDictionary<string, AuditPropertyChange> changes)
        where TEntity : class
        => RecordBulkAudit<TEntity>(context, entityKey, AuditAction.Updated, changes);

    private void RecordBulkAudit<TEntity>(
        DbContext context,
        string entityKey,
        AuditAction action,
        IReadOnlyDictionary<string, AuditPropertyChange> changes)
        where TEntity : class
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        var metadata = AuditMetadataCache.TryGetMetadata(typeof(TEntity));
        if (metadata is null)
        {
            return;
        }

        var sanitizedChanges = SanitizeBulkChanges(changes, metadata);
        if (action == AuditAction.Updated && sanitizedChanges.Count == 0)
        {
            return;
        }

        var record = new EntityAuditRecord(
            Guid.CreateVersion7(),
            timeProvider.GetUtcNow().ToUtc(),
            actorAccessor.GetActorUserId(),
            actorAccessor.GetTraceId(),
            metadata.EntityName,
            entityKey,
            action,
            sanitizedChanges);

        EnqueueOrBufferForTransaction(context, [record]);
    }

    private static IReadOnlyDictionary<string, AuditPropertyChange> SanitizeBulkChanges(
        IReadOnlyDictionary<string, AuditPropertyChange> changes,
        EntityAuditMetadata metadata)
    {
        if (changes.Count == 0)
        {
            return EmptyChanges;
        }

        var sanitized = new Dictionary<string, AuditPropertyChange>(changes.Count, StringComparer.Ordinal);
        foreach (var (propertyName, change) in changes)
        {
            if (metadata.IgnoredProperties.Contains(propertyName))
            {
                continue;
            }

            sanitized[propertyName] = metadata.RedactedProperties.Contains(propertyName)
                ? new AuditPropertyChange(
                    change.OldValue is null ? null : RedactedNode(),
                    change.NewValue is null ? null : RedactedNode())
                : change;
        }

        return sanitized;
    }

    private static Dictionary<string, AuditPropertyChange> ExtractChanges(
        EntityEntry entry,
        EntityAuditMetadata metadata,
        AuditAction action)
    {
        var changes = new Dictionary<string, AuditPropertyChange>(StringComparer.Ordinal);

        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsShadowProperty())
            {
                continue;
            }

            string propertyName = property.Metadata.Name;
            if (metadata.IgnoredProperties.Contains(propertyName))
            {
                continue;
            }

            bool isRedacted = metadata.RedactedProperties.Contains(propertyName);

            switch (action)
            {
                case AuditAction.Created:
                    AppendCreatedProperty(changes, propertyName, property.CurrentValue, isRedacted);
                    break;

                case AuditAction.Deleted:
                    AppendDeletedProperty(changes, propertyName, property.OriginalValue, isRedacted);
                    break;

                case AuditAction.Updated:
                    AppendUpdatedProperty(changes, propertyName, property, isRedacted);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported audit action.");
            }
        }

        return changes;
    }

    private static void AppendCreatedProperty(
        Dictionary<string, AuditPropertyChange> changes,
        string propertyName,
        object? currentValue,
        bool isRedacted)
    {
        if (currentValue is null)
        {
            return;
        }

        if (isRedacted)
        {
            changes[propertyName] = new AuditPropertyChange(null, RedactedNode());
            return;
        }

        if (currentValue is JsonObject jsonObject)
        {
            foreach (var (key, value) in jsonObject)
            {
                if (value is not null)
                {
                    changes[$"{propertyName}.{key}"] = new AuditPropertyChange(null, value.DeepClone());
                }
            }

            return;
        }

        changes[propertyName] = new AuditPropertyChange(null, SerializeValue(currentValue));
    }

    private static void AppendDeletedProperty(
        Dictionary<string, AuditPropertyChange> changes,
        string propertyName,
        object? originalValue,
        bool isRedacted)
    {
        if (originalValue is null)
        {
            return;
        }

        if (isRedacted)
        {
            changes[propertyName] = new AuditPropertyChange(RedactedNode(), null);
            return;
        }

        if (originalValue is JsonObject jsonObject)
        {
            foreach (var (key, value) in jsonObject)
            {
                if (value is not null)
                {
                    changes[$"{propertyName}.{key}"] = new AuditPropertyChange(value.DeepClone(), null);
                }
            }

            return;
        }

        changes[propertyName] = new AuditPropertyChange(SerializeValue(originalValue), null);
    }

    private static void AppendUpdatedProperty(
        Dictionary<string, AuditPropertyChange> changes,
        string propertyName,
        PropertyEntry property,
        bool isRedacted)
    {
        object? originalValue = property.OriginalValue;
        object? currentValue = property.CurrentValue;

        if (originalValue is JsonObject || currentValue is JsonObject)
        {
            AppendJsonObjectDiff(
                changes,
                propertyName,
                originalValue as JsonObject,
                currentValue as JsonObject,
                isRedacted);
            return;
        }

        if (!property.IsModified || AreValuesEqual(originalValue, currentValue))
        {
            return;
        }

        if (isRedacted)
        {
            changes[propertyName] = new AuditPropertyChange(RedactedNode(), RedactedNode());
            return;
        }

        changes[propertyName] = new AuditPropertyChange(
            SerializeValue(originalValue),
            SerializeValue(currentValue));
    }

    private static void AppendJsonObjectDiff(
        Dictionary<string, AuditPropertyChange> changes,
        string propertyName,
        JsonObject? originalObject,
        JsonObject? currentObject,
        bool isRedacted)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        if (originalObject is not null)
        {
            foreach (var (key, _) in originalObject)
            {
                keys.Add(key);
            }
        }

        if (currentObject is not null)
        {
            foreach (var (key, _) in currentObject)
            {
                keys.Add(key);
            }
        }

        foreach (string key in keys)
        {
            JsonNode? oldValue = originalObject is not null && originalObject.TryGetPropertyValue(key, out var o) ? o : null;
            JsonNode? newValue = currentObject is not null && currentObject.TryGetPropertyValue(key, out var n) ? n : null;

            if (JsonNode.DeepEquals(oldValue, newValue))
            {
                continue;
            }

            string path = $"{propertyName}.{key}";
            changes[path] = isRedacted
                ? new AuditPropertyChange(
                    oldValue is null ? null : RedactedNode(),
                    newValue is null ? null : RedactedNode())
                : new AuditPropertyChange(oldValue?.DeepClone(), newValue?.DeepClone());
        }
    }

    private static bool AreValuesEqual(object? left, object? right)
        => (left, right) switch
        {
            _ when ReferenceEquals(left, right) => true,
            (null, _) or (_, null) => false,
            (JsonNode leftNode, JsonNode rightNode) => JsonNode.DeepEquals(leftNode, rightNode),
            (byte[] leftBytes, byte[] rightBytes) => leftBytes.AsSpan().SequenceEqual(rightBytes),
            _ => left.Equals(right),
        };

    private static JsonNode? SerializeValue(object? value)
    {
        return value switch
        {
            null => null,
            JsonNode node => node.DeepClone(),
            string s => JsonValue.Create(s),
            bool b => JsonValue.Create(b),
            byte b => JsonValue.Create(b),
            short s => JsonValue.Create(s),
            int i => JsonValue.Create(i),
            long l => JsonValue.Create(l),
            float f => JsonValue.Create(f),
            double d => JsonValue.Create(d),
            decimal m => JsonValue.Create(m),
            Guid g => JsonValue.Create(g),
            DateTime dt => JsonValue.Create(dt.ToUtc()),
            DateTimeOffset dto => JsonValue.Create(dto.ToUtc()),
            DateOnly d => JsonValue.Create(d.ToString("O", CultureInfo.InvariantCulture)),
            TimeOnly t => JsonValue.Create(t.ToString("O", CultureInfo.InvariantCulture)),
            TimeSpan ts => JsonValue.Create(ts.ToString("c", CultureInfo.InvariantCulture)),
            Enum e => JsonValue.Create(e.ToString()),
            _ => JsonSerializer.SerializeToNode(value, value.GetType(), FallbackSerializerOptions),
        };
    }

    private static bool IsSoftDeleteTransition(EntityEntry entry)
    {
        if (entry.Entity is not ISoftDeletableEntity)
        {
            return false;
        }

        PropertyEntry isDeletedProperty = entry.Property(nameof(ISoftDeletableEntity.IsDeleted));
        return isDeletedProperty.IsModified
            && isDeletedProperty.OriginalValue is false
            && isDeletedProperty.CurrentValue is true;
    }

    private static JsonValue RedactedNode() => JsonValue.Create(RedactedPlaceholder);

    private static string BuildEntityKey(EntityEntry entry)
    {
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null || primaryKey.Properties.Count == 0)
        {
            return string.Empty;
        }

        if (primaryKey.Properties.Count == 1)
        {
            object? value = entry.Property(primaryKey.Properties[0].Name).CurrentValue
                            ?? entry.Property(primaryKey.Properties[0].Name).OriginalValue;
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        var parts = new string[primaryKey.Properties.Count];
        for (int i = 0; i < primaryKey.Properties.Count; i++)
        {
            var prop = entry.Property(primaryKey.Properties[i].Name);
            object? value = prop.CurrentValue ?? prop.OriginalValue;
            parts[i] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return string.Join(':', parts);
    }

    private void CapturePendingEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        _pendingEntries.Remove(context);

        if (!options.Value.Enabled)
        {
            return;
        }

        List<PendingAuditEntry>? pending = null;
        DateTimeOffset occurredAtUtc = default;
        Guid? actorUserId = null;
        string? traceId = null;
        bool contextResolved = false;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entry.Metadata.IsOwned())
            {
                continue;
            }

            var metadata = AuditMetadataCache.TryGetMetadata(entry.Entity.GetType());
            if (metadata is null)
            {
                continue;
            }

            bool isSoftDelete = entry.State == EntityState.Modified && IsSoftDeleteTransition(entry);

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Modified when isSoftDelete => AuditAction.Deleted,
                EntityState.Modified => AuditAction.Updated,
                EntityState.Deleted => AuditAction.Deleted,
                EntityState.Detached or EntityState.Unchanged or _ => throw new InvalidOperationException($"Unsupported entity state: {entry.State}."),
            };

            var changes = ExtractChanges(entry, metadata, isSoftDelete ? AuditAction.Updated : action);
            if (action == AuditAction.Updated && changes.Count == 0)
            {
                continue;
            }

            if (!contextResolved)
            {
                occurredAtUtc = timeProvider.GetUtcNow().ToUtc();
                actorUserId = actorAccessor.GetActorUserId();
                traceId = actorAccessor.GetTraceId();
                contextResolved = true;
            }

            // Capture keys up front for Updated/Deleted (Deleted entries become Detached after SaveChanges).
            // For Added entries, defer key resolution until SavedChanges so DB/value-generator keys are populated.
            string? precomputedKey = action == AuditAction.Created ? null : BuildEntityKey(entry);

            pending ??= [];
            pending.Add(new PendingAuditEntry(
                entry,
                metadata.EntityName,
                precomputedKey,
                action,
                changes,
                occurredAtUtc,
                actorUserId,
                traceId));
        }

        if (pending is { Count: > 0 })
        {
            _pendingEntries.Add(context, pending);
        }
    }

    private void FlushPendingEntries(DbContext? context)
    {
        if (context is null || !_pendingEntries.TryGetValue(context, out var pending))
        {
            return;
        }

        _pendingEntries.Remove(context);

        var records = new List<EntityAuditRecord>(pending.Count);
        foreach (var item in pending)
        {
            string entityKey = item.PrecomputedEntityKey ?? BuildEntityKey(item.Entry);
            records.Add(new EntityAuditRecord(
                Guid.CreateVersion7(),
                item.OccurredAtUtc,
                item.ActorUserId,
                item.TraceId,
                item.EntityName,
                entityKey,
                item.Action,
                item.Changes));
        }

        EnqueueOrBufferForTransaction(context, records);
    }

    private void EnqueueOrBufferForTransaction(DbContext context, List<EntityAuditRecord> records)
    {
        if (records.Count == 0)
        {
            return;
        }

        DbTransaction? activeTransaction = context.Database.CurrentTransaction?.GetDbTransaction();
        if (activeTransaction is null)
        {
            auditLogBuffer.EnqueueRange(records);
            return;
        }

        var transactionBuffer = _transactionRecords.GetOrCreateValue(activeTransaction);
        lock (transactionBuffer)
        {
            transactionBuffer.AddRange(records);
        }
    }

    private void FlushTransactionRecords(DbTransaction transaction)
    {
        if (!_transactionRecords.TryGetValue(transaction, out var transactionBuffer))
        {
            return;
        }

        _transactionRecords.Remove(transaction);

        List<EntityAuditRecord> snapshot;
        lock (transactionBuffer)
        {
            snapshot = [.. transactionBuffer];
            transactionBuffer.Clear();
        }

        if (snapshot.Count > 0)
        {
            auditLogBuffer.EnqueueRange(snapshot);
        }
    }

    private void DiscardTransactionRecords(DbTransaction transaction)
    {
        if (_transactionRecords.TryGetValue(transaction, out var transactionBuffer))
        {
            _transactionRecords.Remove(transaction);
            lock (transactionBuffer)
            {
                transactionBuffer.Clear();
            }
        }
    }

    private void DiscardPendingEntries(DbContext? context)
    {
        if (context is not null)
        {
            _pendingEntries.Remove(context);
        }
    }

    private sealed record PendingAuditEntry(
        EntityEntry Entry,
        string EntityName,
        string? PrecomputedEntityKey,
        AuditAction Action,
        Dictionary<string, AuditPropertyChange> Changes,
        DateTimeOffset OccurredAtUtc,
        Guid? ActorUserId,
        string? TraceId);
}
