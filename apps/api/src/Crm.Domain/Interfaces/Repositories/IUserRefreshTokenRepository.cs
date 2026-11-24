namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines the contract for managing user refresh tokens in the database.
/// </summary>
public interface IUserRefreshTokenRepository
{
    /// <summary>
    /// Asynchronously adds a new refresh token to the database.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="refreshToken">The refresh token string to add.</param>
    /// <param name="expiresAt">The UTC date and time with offset when the token expires.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task CreateAsync(Guid userId, string refreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes all expired refresh tokens from the database.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task RemoveExpiredTokensAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically verifies and consumes a valid (non-expired) refresh token belonging to the specified user
    /// by deleting the matching database row in a single operation, preventing concurrent token-replay races.
    /// </summary>
    /// <param name="userId">The unique identifier of the user who owns the token.</param>
    /// <param name="refreshToken">The raw refresh token string to consume.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task whose result is <see langword="true"/> if a matching, non-expired token existed and was consumed;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    Task<bool> TryConsumeValidTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes a specific refresh token from the database.
    /// This is typically used for token rotation or user logout.
    /// </summary>
    /// <param name="refreshToken">The refresh token string to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous remove operation.</returns>
    Task RemoveTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes all refresh tokens associated with a specific user.
    /// This is typically used during password resets, security revocations, or global logouts.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose tokens are to be revoked.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous remove operation.</returns>
    Task RevokeAllUserTokensAsync(Guid userId, CancellationToken cancellationToken = default);
}
