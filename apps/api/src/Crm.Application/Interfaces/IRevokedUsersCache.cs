namespace Crm.Application.Interfaces;

/// <summary>
/// Defines a dedicated cache for users whose active access tokens must be immediately rejected
/// (for example, after account deactivation or deletion) for the configured access token lifetime.
/// Designed with asynchronous signatures to facilitate seamless future migration to distributed caching (e.g., Redis).
/// </summary>
public interface IRevokedUsersCache
{
    /// <summary>
    /// Adds the specified user to the revoked users cache for the duration of the access token lifetime.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to revoke.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous cache update operation.</returns>
    Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the specified user from the revoked users cache (for example, when a deactivated account is re-activated).
    /// </summary>
    /// <param name="userId">The unique identifier of the user to restore.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous cache removal operation.</returns>
    Task UnrevokeUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the specified user is currently present in the revoked users cache.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task resolving to <see langword="true"/> if the user is currently revoked; otherwise, <see langword="false"/>.
    /// </returns>
    Task<bool> IsUserRevokedAsync(Guid userId, CancellationToken cancellationToken = default);
}
