using Crm.Application.Dtos.Users;
using Crm.Domain.Common;
using Crm.Domain.Enums;

namespace Crm.Application.Interfaces;

/// <summary>
/// High-level service managing the full lifecycle of short opaque user action tokens
/// (issuance with AES-256-GCM encrypted metadata, resolution, paged listing, atomic single-use consumption, and revocation).
/// </summary>
public interface IUserActionTokenService
{
    /// <summary>
    /// Generates a short opaque token, symmetrically encrypts <paramref name="metadata"/>,
    /// persists the hashed token record, and returns the raw opaque token with its expiration timestamp.
    /// </summary>
    /// <typeparam name="TMetadata">The typed metadata payload associated with the token.</typeparam>
    /// <param name="type">The functional category of the action token.</param>
    /// <param name="targetEmail">The target email address associated with this token.</param>
    /// <param name="metadata">The typed metadata to encrypt and store with the token.</param>
    /// <param name="targetUserId">Optional existing user identifier (for existing-user flows such as password resets).</param>
    /// <param name="createdBy">Optional identifier of the acting user who issued the token.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The generated raw opaque token and its UTC+0 expiration timestamp.</returns>
    Task<TokenResultDto> IssueAsync<TMetadata>(
        ActionTokenType type,
        string targetEmail,
        TMetadata metadata,
        Guid? targetUserId = null,
        Guid? createdBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up an active (unexpired) action token by its raw opaque value and decrypts/authenticates its metadata.
    /// </summary>
    /// <typeparam name="TMetadata">The expected typed metadata payload.</typeparam>
    /// <param name="rawToken">The raw opaque token provided by the client.</param>
    /// <param name="type">The expected action token category.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="ResolvedActionToken{TMetadata}"/> containing the lean token projection and decrypted metadata,
    /// or <see langword="null"/> if the token is missing, expired, or has invalid/tampered metadata.
    /// </returns>
    Task<ResolvedActionToken<TMetadata>?> ResolveActiveAsync<TMetadata>(
        string rawToken,
        ActionTokenType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated, filtered, and sorted page of active (unexpired) action tokens along with their decrypted metadata.
    /// </summary>
    /// <typeparam name="TMetadata">The expected typed metadata payload.</typeparam>
    /// <param name="type">The action token category to query.</param>
    /// <param name="filter">The filtering, sorting, and pagination parameters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated collection of resolved active action tokens.</returns>
    Task<PagedResult<ResolvedActionToken<TMetadata>>> GetPagedActiveWithMetadataAsync<TMetadata>(
        ActionTokenType type,
        UserActionTokenFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated, filtered, and sorted page of active (unexpired) action tokens projected to <typeparamref name="TResult"/>
    /// without decrypting their metadata.
    /// </summary>
    /// <typeparam name="TResult">The target projection DTO type.</typeparam>
    /// <param name="type">The action token category to query.</param>
    /// <param name="filter">The filtering, sorting, and pagination parameters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated collection of projected active action tokens.</returns>
    Task<PagedResult<TResult>> GetPagedActiveAsync<TResult>(
        ActionTokenType type,
        UserActionTokenFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically consumes an active action token by deleting its database row in a single operation.
    /// </summary>
    /// <param name="id">The unique identifier of the action token.</param>
    /// <param name="type">The expected action token category.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if an unexpired matching token was consumed; otherwise, <see langword="false"/>.</returns>
    Task<bool> TryConsumeAsync(Guid id, ActionTokenType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently revokes an action token by its identifier and type, throwing <see cref="Domain.Exceptions.EntityNotFoundException{UserActionToken}"/>
    /// when no matching token exists.
    /// </summary>
    /// <param name="id">The unique identifier of the token to revoke.</param>
    /// <param name="type">The expected action token category.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous revocation operation.</returns>
    Task RevokeByIdAsync(Guid id, ActionTokenType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes all action tokens of the specified type targeting the given email address (case-insensitive).
    /// </summary>
    /// <param name="email">The target email address.</param>
    /// <param name="type">The action token category to revoke.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous revocation operation.</returns>
    Task RevokeAllByTargetEmailAsync(string email, ActionTokenType type, CancellationToken cancellationToken = default);
}
