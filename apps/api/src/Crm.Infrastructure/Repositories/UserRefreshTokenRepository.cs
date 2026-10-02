using System.Security.Cryptography;
using System.Text;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository implementation for managing <see cref="UserRefreshToken"/> entities.
/// </summary>
public class UserRefreshTokenRepository(
    ApplicationDbContext context,
    TimeProvider timeProvider)
    : IUserRefreshTokenRepository
{
    /// <summary>
    /// Gets the <see cref="DbSet{TEntity}"/> for managing <see cref="UserRefreshToken"/> entities.
    /// </summary>
    private DbSet<UserRefreshToken> DbSet { get; } = context.Set<UserRefreshToken>();

    /// <summary>
    /// Asynchronously adds a new refresh token to the database.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="refreshToken">The refresh token string to add.</param>
    /// <param name="expiresAt">The date and time when the token expires.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public async Task CreateAsync(Guid userId, string refreshToken, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        var tokenEntity = new UserRefreshToken
        {
            UserId = userId,
            Token = HashToken(refreshToken),
            ExpiresAt = expiresAt,
        };

        await DbSet.AddAsync(tokenEntity, cancellationToken);
    }

    /// <summary>
    /// Asynchronously removes all expired refresh tokens from the database.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task RemoveExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        return DbSet
            .Where(rt => rt.ExpiresAt < timeProvider.GetUtcNow().UtcDateTime)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Asynchronously checks if the provided refresh token is valid, meaning it exists, belongs to the specified user, and has not expired.
    /// </summary>
    /// <param name="userId">The unique identifier of the user who owns the token.</param>
    /// <param name="refreshToken">The refresh token string to validate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task whose result is <c>true</c> if the token is valid; otherwise, <c>false</c>.</returns>
    public Task<bool> IsValidTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        return DbSet.AnyAsync(
            rt =>
            rt.UserId == userId &&
            rt.Token == HashToken(refreshToken) &&
            rt.ExpiresAt > timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously removes a specific refresh token from the database.
    /// This is typically used for token rotation or user logout.
    /// </summary>
    /// <param name="refreshToken">The refresh token string to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous remove operation.</returns>
    public Task RemoveTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Where(rt => rt.Token == HashToken(refreshToken))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
