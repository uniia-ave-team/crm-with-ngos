using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines data access operations for storing, projecting, querying, consuming, and revoking <see cref="UserActionToken"/> records.
/// </summary>
public interface IUserActionTokenRepository
{
    /// <summary>
    /// Hashes <paramref name="rawToken"/> and adds the <paramref name="token"/> record to the database context.
    /// </summary>
    /// <param name="token">The action token metadata entity to persist.</param>
    /// <param name="rawToken">The raw opaque token returned to the client; only its SHA-256 hash is stored.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous add operation.</returns>
    Task CreateAsync(UserActionToken token, string rawToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves and projects an active (unexpired) action token matching the specified raw token and type via Mapster.
    /// </summary>
    /// <typeparam name="TResult">The target projection type.</typeparam>
    /// <param name="rawToken">The raw opaque token provided by the client.</param>
    /// <param name="type">The expected <see cref="ActionTokenType"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The projected active token, or <see langword="null"/> if not found or expired.</returns>
    Task<TResult?> GetActiveByRawTokenAsync<TResult>(
        string rawToken,
        ActionTokenType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted page of active (unexpired) action tokens projected to <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">The target projection type.</typeparam>
    /// <param name="type">The <see cref="ActionTokenType"/> to query.</param>
    /// <param name="filter">The filtering, sorting, and pagination parameters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated collection of projected action tokens.</returns>
    Task<PagedResult<TResult>> GetPagedActiveAsync<TResult>(
        ActionTokenType type,
        UserActionTokenFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically consumes an active (unexpired) action token by deleting its database row in a single operation,
    /// preventing concurrent replay races.
    /// </summary>
    /// <param name="id">The unique identifier of the action token.</param>
    /// <param name="type">The expected <see cref="ActionTokenType"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// <see langword="true"/> if an unexpired token with the specified identifier and type existed and was consumed;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    Task<bool> TryConsumeAsync(Guid id, ActionTokenType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously revokes (deletes) an action token by its identifier and type.
    /// </summary>
    /// <param name="id">The unique identifier of the action token to revoke.</param>
    /// <param name="type">The expected <see cref="ActionTokenType"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if a matching token was deleted; otherwise, <see langword="false"/>.</returns>
    Task<bool> DeleteByIdAsync(Guid id, ActionTokenType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously revokes all action tokens of the specified type targeting the given email address.
    /// </summary>
    /// <param name="email">The target email address (case-insensitive).</param>
    /// <param name="type">The <see cref="ActionTokenType"/> to revoke.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous revocation operation.</returns>
    Task RevokeAllByTargetEmailAsync(string email, ActionTokenType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously revokes all action tokens of the specified type targeting the given user identifier.
    /// </summary>
    /// <param name="userId">The target user identifier.</param>
    /// <param name="type">The <see cref="ActionTokenType"/> to revoke.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous revocation operation.</returns>
    Task RevokeAllByTargetUserIdAsync(Guid userId, ActionTokenType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes all expired action tokens from the database.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of expired token rows deleted.</returns>
    Task<int> RemoveExpiredTokensAsync(CancellationToken cancellationToken = default);
}
