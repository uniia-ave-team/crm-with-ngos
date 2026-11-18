using Crm.Application.Interfaces;

namespace Crm.Api.DemoMode;

/// <summary>
/// Decorator around <see cref="IRolePermissionsCache"/> that prevents command-triggered cache
/// updates and evictions while Demo Mode is active, while keeping reads and startup seeding operational.
/// </summary>
/// <param name="inner">The underlying <see cref="IRolePermissionsCache"/> implementation.</param>
/// <param name="demoModeState">The service providing current Demo Mode state.</param>
public sealed class DemoRolePermissionsCache(
    IRolePermissionsCache inner,
    IDemoModeState demoModeState) : IRolePermissionsCache
{
    /// <inheritdoc />
    public Task<IReadOnlySet<string>> GetRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
        => inner.GetRolePermissionsAsync(roleId, cancellationToken);

    /// <inheritdoc />
    public Task SetRolePermissionsAsync(Guid roleId, HashSet<string> rights, CancellationToken cancellationToken = default)
        => demoModeState.IsActive
            ? Task.CompletedTask
            : inner.SetRolePermissionsAsync(roleId, rights, cancellationToken);

    /// <inheritdoc />
    public Task RemoveRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
        => demoModeState.IsActive
            ? Task.CompletedTask
            : inner.RemoveRolePermissionsAsync(roleId, cancellationToken);

    /// <inheritdoc />
    public Task SeedAllRolesCacheAsync(CancellationToken cancellationToken = default)
        => inner.SeedAllRolesCacheAsync(cancellationToken);
}
