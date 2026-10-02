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
    /// <param name="expiresAt">The date and time when the token expires.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task CreateAsync(Guid userId, string refreshToken, DateTime expiresAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes all expired refresh tokens from the database.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task RemoveExpiredTokensAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks if the provided refresh token is valid, meaning it exists, belongs to the specified user, and has not expired.
    /// </summary>
    /// <param name="userId">The unique identifier of the user who owns the token.</param>
    /// <param name="refreshToken">The refresh token string to validate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task whose result is <c>true</c> if the token is valid; otherwise, <c>false</c>.</returns>
    Task<bool> IsValidTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes a specific refresh token from the database.
    /// This is typically used for token rotation or user logout.
    /// </summary>
    /// <param name="refreshToken">The refresh token string to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous remove operation.</returns>
    Task RemoveTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}
