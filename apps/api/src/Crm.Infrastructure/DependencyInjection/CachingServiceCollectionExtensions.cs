using Crm.Application.Interfaces;
using Crm.Infrastructure.Services.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.DependencyInjection;

/// <summary>
/// Provides dependency injection registration for <see cref="Microsoft.Extensions.Caching.Hybrid.HybridCache"/>
/// and singleton domain caches.
/// </summary>
public static class CachingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="Microsoft.Extensions.Caching.Hybrid.HybridCache"/> (L1 in-memory with stampede protection
    /// and seamless optional L2 distributed cache support) along with singleton domain caches.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddCaching(this IServiceCollection services)
    {
        services.AddHybridCache();
        services.AddSingleton<IRolePermissionsCache, RolePermissionsCache>();
        services.AddSingleton<IMaintenanceNoticeCache, MaintenanceNoticeCache>();
        services.AddSingleton<IRevokedUsersCache, RevokedUsersCache>();

        return services;
    }
}
