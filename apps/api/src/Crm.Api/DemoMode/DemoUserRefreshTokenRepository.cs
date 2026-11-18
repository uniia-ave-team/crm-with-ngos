using Crm.Domain.Interfaces.Repositories;

namespace Crm.Api.DemoMode;

/// <summary>
/// Decorator around <see cref="IUserRefreshTokenRepository"/> that routes Demo Mode authentication
/// refresh tokens to the bounded in-memory <see cref="DemoRefreshTokenStore"/> instead of writing to PostgreSQL.
/// Suppresses token creation/revocation during dry-run mutations so no visitor can lock or invalidate active demo sessions.
/// </summary>
/// <param name="inner">The underlying <see cref="IUserRefreshTokenRepository"/> implementation.</param>
/// <param name="store">The bounded in-memory refresh token store.</param>
/// <param name="demoModeState">The service providing current Demo Mode state.</param>
/// <param name="executionContext">The active Demo Mode execution context.</param>
public sealed class DemoUserRefreshTokenRepository(
    IUserRefreshTokenRepository inner,
    DemoRefreshTokenStore store,
    IDemoModeState demoModeState,
    IDemoExecutionContext executionContext) : IUserRefreshTokenRepository
{
    /// <inheritdoc />
    public Task CreateAsync(Guid userId, string refreshToken, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        if (!demoModeState.IsActive)
        {
            return inner.CreateAsync(userId, refreshToken, expiresAt, cancellationToken);
        }

        if (!executionContext.IsDryRunActive)
        {
            store.Add(userId, refreshToken, expiresAt);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        if (!demoModeState.IsActive)
        {
            return inner.RemoveExpiredTokensAsync(cancellationToken);
        }

        store.RemoveExpired();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> IsValidTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
        => !demoModeState.IsActive
            ? inner.IsValidTokenAsync(userId, refreshToken, cancellationToken)
            : Task.FromResult(store.IsValid(userId, refreshToken));

    /// <inheritdoc />
    public Task RemoveTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (!demoModeState.IsActive)
        {
            return inner.RemoveTokenAsync(refreshToken, cancellationToken);
        }

        if (!executionContext.IsDryRunActive)
        {
            store.Remove(refreshToken);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RevokeAllUserTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!demoModeState.IsActive)
        {
            return inner.RevokeAllUserTokensAsync(userId, cancellationToken);
        }

        // Never revoke active demo sessions or delete DB rows during Demo Mode.
        return Task.CompletedTask;
    }
}
