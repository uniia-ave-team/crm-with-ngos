using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository implementation for managing <see cref="UserRefreshToken"/> entities.
/// </summary>
/// <param name="context">The application database context.</param>
/// <param name="timeProvider">The time provider supplying UTC timestamps for token expiration checks.</param>
public sealed class UserRefreshTokenRepository(
    ApplicationDbContext context,
    TimeProvider timeProvider)
    : IUserRefreshTokenRepository
{
    private DbSet<UserRefreshToken> DbSet { get; } = context.Set<UserRefreshToken>();

    /// <inheritdoc />
    public async Task CreateAsync(Guid userId, string refreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var tokenEntity = new UserRefreshToken
        {
            UserId = userId,
            Token = TokenHashHelper.Hash(refreshToken),
            ExpiresAt = expiresAt.ToUniversalTime(),
        };

        await DbSet.AddAsync(tokenEntity, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset utcNow = timeProvider.GetUtcNow();

        return DbSet
            .Where(rt => rt.ExpiresAt < utcNow)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> TryConsumeValidTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        string tokenHash = TokenHashHelper.Hash(refreshToken);
        DateTimeOffset utcNow = timeProvider.GetUtcNow();

        int rowsAffected = await DbSet
            .Where(rt =>
                rt.UserId == userId &&
                rt.Token == tokenHash &&
                rt.ExpiresAt > utcNow)
            .ExecuteDeleteAsync(cancellationToken);

        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public Task RemoveTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        string tokenHash = TokenHashHelper.Hash(refreshToken);

        return DbSet
            .Where(rt => rt.Token == tokenHash)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task RevokeAllUserTokensAsync(Guid userId, CancellationToken cancellationToken = default)
        => DbSet
            .Where(rt => rt.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
}
