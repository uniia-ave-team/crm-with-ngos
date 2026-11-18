using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Crm.Api.DemoMode;

/// <summary>
/// Thread-safe, bounded in-memory store for refresh tokens issued during Demo Mode sessions.
/// Prevents public demo logins from writing to or bloating the PostgreSQL <c>UserRefreshTokens</c> table
/// while preserving per-visitor login, token rotation, and isolated logout behavior.
/// </summary>
/// <param name="demoModeOptions">The Demo Mode configuration options defining the maximum capacity.</param>
/// <param name="timeProvider">The time provider used to evaluate token expiration.</param>
public sealed class DemoRefreshTokenStore(
    IOptions<DemoModeOptions> demoModeOptions,
    TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, DemoRefreshTokenEntry> _tokens = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _insertionOrder = new();
    private readonly int _maxCapacity = Math.Max(1, demoModeOptions.Value.MaxInMemoryRefreshTokens);

    /// <summary>
    /// Gets the current number of stored active refresh tokens.
    /// </summary>
    public int Count => _tokens.Count;

    /// <summary>
    /// Adds a hashed refresh token entry to the bounded in-memory store, evicting expired or oldest entries if capacity is reached.
    /// </summary>
    /// <param name="userId">The unique identifier of the user who owns the token.</param>
    /// <param name="rawRefreshToken">The raw refresh token string.</param>
    /// <param name="expiresAt">The UTC expiration timestamp of the token.</param>
    public void Add(Guid userId, string rawRefreshToken, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return;
        }

        string hash = HashToken(rawRefreshToken);

        if (_tokens.Count >= _maxCapacity)
        {
            RemoveExpired();
            EvictOldestIfOverCapacity();
        }

        _tokens[hash] = new DemoRefreshTokenEntry(userId, expiresAt);
        _insertionOrder.Enqueue(hash);
    }

    /// <summary>
    /// Determines whether the provided refresh token exists for the specified user and has not expired.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="rawRefreshToken">The raw refresh token string to validate.</param>
    /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
    public bool IsValid(Guid userId, string rawRefreshToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return false;
        }

        string hash = HashToken(rawRefreshToken);
        if (!_tokens.TryGetValue(hash, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt <= timeProvider.GetUtcNow().UtcDateTime)
        {
            _tokens.TryRemove(hash, out _);
            return false;
        }

        return entry.UserId == userId;
    }

    /// <summary>
    /// Removes a specific refresh token from the in-memory store (used for token rotation or isolated single-visitor logout).
    /// </summary>
    /// <param name="rawRefreshToken">The raw refresh token string to remove.</param>
    public void Remove(string rawRefreshToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return;
        }

        _tokens.TryRemove(HashToken(rawRefreshToken), out _);
    }

    /// <summary>
    /// Removes all expired refresh tokens from the in-memory store.
    /// </summary>
    public void RemoveExpired()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var kvp in _tokens)
        {
            if (kvp.Value.ExpiresAt <= now)
            {
                _tokens.TryRemove(kvp.Key, out _);
            }
        }
    }

    private void EvictOldestIfOverCapacity()
    {
        while (_tokens.Count >= _maxCapacity && _insertionOrder.TryDequeue(out var oldestHash))
        {
            _tokens.TryRemove(oldestHash, out _);
        }
    }

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private readonly record struct DemoRefreshTokenEntry(Guid UserId, DateTime ExpiresAt);
}
