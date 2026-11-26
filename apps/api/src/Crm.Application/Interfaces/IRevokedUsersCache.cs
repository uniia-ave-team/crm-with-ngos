namespace Crm.Application.Interfaces;

/// <summary>
/// Defines a dedicated cache for users whose active access tokens must be immediately rejected
/// (either completely after account deactivation/deletion, or selectively for tokens issued before a role change)
/// for the configured access token lifetime.
/// Designed with asynchronous signatures to facilitate seamless future migration to distributed caching (e.g., Redis).
/// </summary>
public interface IRevokedUsersCache
{
    /// <summary>
    /// Adds the specified user to the revoked users cache for the duration of the access token lifetime,
    /// rejecting all access tokens regardless of issuance time (used on account deactivation or deletion).
    /// </summary>
    /// <param name="userId">The unique identifier of the user to revoke.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous cache update operation.</returns>
    Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all access tokens issued for the specified user at or before the current UTC timestamp as stale
    /// for the duration of the access token lifetime (used when the user's assigned roles change).
    /// Newly refreshed access tokens issued after this point remain valid so the client can seamlessly refresh.
    /// Does not downgrade an existing full account revocation created by <see cref="RevokeUserAsync"/>.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose prior access tokens should be invalidated.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous cache update operation.</returns>
    Task RevokeTokensIssuedBeforeAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the specified user from the revoked users cache (for example, when a deactivated account is re-activated).
    /// </summary>
    /// <param name="userId">The unique identifier of the user to restore.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous cache removal operation.</returns>
    Task UnrevokeUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the specified user is currently fully revoked (via <see cref="RevokeUserAsync"/>).
    /// </summary>
    /// <param name="userId">The unique identifier of the user to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task resolving to <see langword="true"/> if the user is currently fully revoked; otherwise, <see langword="false"/>.
    /// </returns>
    Task<bool> IsUserRevokedAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a specific access token belonging to <paramref name="userId"/> is revoked —
    /// either because the user is fully revoked, or because the token was issued at or before
    /// the user's cached <c>RevokedBefore</c> timestamp (e.g., following a role change).
    /// </summary>
    /// <param name="userId">The unique identifier of the user who owns the token.</param>
    /// <param name="tokenIssuedAt">The UTC timestamp when the access token was issued, or <see langword="null"/> if absent.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task resolving to <see langword="true"/> if the token must be rejected with <c>401 Unauthorized</c>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    Task<bool> IsTokenRevokedAsync(Guid userId, DateTimeOffset? tokenIssuedAt, CancellationToken cancellationToken = default);
}
