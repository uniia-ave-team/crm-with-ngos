using Crm.Application.Interfaces;

namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// Keeps revoked users separately for every sandbox. Every sandbox is a copy of the same template, so a demo
/// colleague has the same id in all of them: the process-wide cache (keyed by user id only) would let a visitor
/// who deactivates or deletes a colleague, or changes their roles, in their own sandbox sign that colleague out
/// for every other visitor. Requests without a sandbox use the regular cache.
/// </summary>
/// <remarks>
/// The access token check runs during authentication, before <see cref="DemoModeMiddleware"/> picks the sandbox,
/// so <see cref="DemoSandboxJwtBearerEvents"/> checks the sandbox named in the token itself.
/// </remarks>
/// <param name="inner">The regular revoked users cache.</param>
/// <param name="sandboxAccessor">The accessor of the current sandbox.</param>
/// <param name="timeProvider">The clock that also stamps the access tokens (their issue time is compared with it).</param>
public sealed class DemoSandboxRevokedUsersCache(
    IRevokedUsersCache inner,
    IDemoSandboxAccessor sandboxAccessor,
    TimeProvider timeProvider) : IRevokedUsersCache
{
    /// <inheritdoc />
    public Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sandbox = sandboxAccessor.Current;

        if (sandbox is null)
        {
            return inner.RevokeUserAsync(userId, cancellationToken);
        }

        sandbox.RevokeUser(userId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RevokeTokensIssuedBeforeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sandbox = sandboxAccessor.Current;

        if (sandbox is null)
        {
            return inner.RevokeTokensIssuedBeforeAsync(userId, cancellationToken);
        }

        sandbox.RevokeTokensIssuedBefore(userId, timeProvider.GetUtcNow());

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UnrevokeUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sandbox = sandboxAccessor.Current;

        if (sandbox is null)
        {
            return inner.UnrevokeUserAsync(userId, cancellationToken);
        }

        sandbox.UnrevokeUser(userId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> IsUserRevokedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sandbox = sandboxAccessor.Current;

        return sandbox is null
            ? inner.IsUserRevokedAsync(userId, cancellationToken)
            : Task.FromResult(sandbox.IsUserRevoked(userId));
    }

    /// <inheritdoc />
    public Task<bool> IsTokenRevokedAsync(Guid userId, DateTimeOffset? tokenIssuedAt, CancellationToken cancellationToken = default)
    {
        var sandbox = sandboxAccessor.Current;

        return sandbox is null
            ? inner.IsTokenRevokedAsync(userId, tokenIssuedAt, cancellationToken)
            : Task.FromResult(sandbox.IsTokenRevoked(userId, tokenIssuedAt));
    }
}
