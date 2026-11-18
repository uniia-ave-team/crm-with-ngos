using System.Collections.Concurrent;

namespace Crm.Api.DemoMode;

/// <summary>
/// Bounded, short-lived registry of entity identifiers returned by dry-run creation commands in Demo Mode.
/// Enables follow-up write operations against recently created dry-run entities to succeed gracefully
/// without masking genuine <c>404 Not Found</c> responses for unrelated identifiers.
/// </summary>
/// <param name="timeProvider">The time provider used to expire tracked identifiers.</param>
public sealed class DemoDryRunIdRegistry(TimeProvider timeProvider)
{
    private const int MaxEntries = 2_000;
    private static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<(string Kind, Guid Id), DateTime> _entries = new();
    private readonly ConcurrentQueue<(string Kind, Guid Id)> _insertionOrder = new();

    /// <summary>
    /// Records an entity identifier produced by a dry-run create command.
    /// </summary>
    /// <param name="kind">The logical entity category (for example <c>"Role"</c>, <c>"CustomTable"</c>).</param>
    /// <param name="id">The dry-run generated entity identifier.</param>
    public void Register(string kind, Guid id)
    {
        if (string.IsNullOrWhiteSpace(kind) || id == Guid.Empty)
        {
            return;
        }

        if (_entries.Count >= MaxEntries)
        {
            CleanupExpired();
            while (_entries.Count >= MaxEntries && _insertionOrder.TryDequeue(out var oldest))
            {
                _entries.TryRemove(oldest, out _);
            }
        }

        var key = (kind, id);
        _entries[key] = timeProvider.GetUtcNow().UtcDateTime.Add(EntryTtl);
        _insertionOrder.Enqueue(key);
    }

    /// <summary>
    /// Determines whether the specified entity identifier was recently created during a Demo Mode dry-run.
    /// </summary>
    /// <param name="kind">The logical entity category.</param>
    /// <param name="id">The entity identifier to check.</param>
    /// <returns><see langword="true"/> if tracked and not expired; otherwise, <see langword="false"/>.</returns>
    public bool Contains(string kind, Guid id)
    {
        if (string.IsNullOrWhiteSpace(kind) || id == Guid.Empty)
        {
            return false;
        }

        var key = (kind, id);
        if (!_entries.TryGetValue(key, out var expiresAt))
        {
            return false;
        }

        if (expiresAt <= timeProvider.GetUtcNow().UtcDateTime)
        {
            _entries.TryRemove(key, out _);
            return false;
        }

        return true;
    }

    private void CleanupExpired()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var kvp in _entries)
        {
            if (kvp.Value <= now)
            {
                _entries.TryRemove(kvp.Key, out _);
            }
        }
    }
}
