using System.Collections.Frozen;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;

namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// Keeps role permissions separately for every sandbox. Visitors can edit roles inside their sandbox,
/// so the process-wide cache (keyed by role only) would leak one visitor's changes to all others.
/// Requests without a sandbox use the regular cache.
/// </summary>
/// <param name="inner">The regular permission cache.</param>
/// <param name="sandboxAccessor">The accessor of the current sandbox.</param>
/// <param name="httpContextAccessor">The HTTP context accessor.</param>
public sealed class DemoSandboxRolePermissionsCache(
    IRolePermissionsCache inner,
    IDemoSandboxAccessor sandboxAccessor,
    IHttpContextAccessor httpContextAccessor) : IRolePermissionsCache
{
    private static readonly FrozenSet<string> NoPermissions = [];

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var sandbox = sandboxAccessor.Current;

        if (sandbox is null)
        {
            return await inner.GetRolePermissionsAsync(roleId, cancellationToken);
        }

        if (!sandbox.RolePermissionsLoaded)
        {
            await LoadAsync(sandbox, cancellationToken);
        }

        return sandbox.RolePermissions.TryGetValue(roleId, out var rights) ? rights : NoPermissions;
    }

    /// <inheritdoc />
    public Task SetRolePermissionsAsync(Guid roleId, HashSet<string> rights, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rights);

        var sandbox = sandboxAccessor.Current;

        if (sandbox is null)
        {
            return inner.SetRolePermissionsAsync(roleId, rights, cancellationToken);
        }

        sandbox.RolePermissions[roleId] = rights.ToFrozenSet();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var sandbox = sandboxAccessor.Current;

        if (sandbox is null)
        {
            return inner.RemoveRolePermissionsAsync(roleId, cancellationToken);
        }

        sandbox.RolePermissions.TryRemove(roleId, out _);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SeedAllRolesCacheAsync(CancellationToken cancellationToken = default)
        => inner.SeedAllRolesCacheAsync(cancellationToken);

    private async Task LoadAsync(DemoSandbox sandbox, CancellationToken cancellationToken)
    {
        // The request's own scope is used on purpose: its DbContext already points at the sandbox database.
        var services = httpContextAccessor.HttpContext?.RequestServices
            ?? throw new InvalidOperationException("A sandbox exists only inside an HTTP request.");

        var roleRepository = services.GetRequiredService<IAuthRoleRepository>();
        var permissions = await roleRepository.GetAllRolesPermissionsAsync(cancellationToken);

        foreach (var (roleId, rights) in permissions)
        {
            sandbox.RolePermissions.TryAdd(roleId, rights.ToFrozenSet());
        }

        sandbox.MarkRolePermissionsLoaded();
    }
}
