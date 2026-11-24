using Crm.Application.Interfaces;
using Crm.Infrastructure.Services.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.DependencyInjection;

/// <summary>
/// Provides dependency injection registration for in-memory infrastructure caches.
/// </summary>
public static class CachingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/> and singleton domain caches.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddCaching(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IRolePermissionsCache, RolePermissionsCache>();
        services.AddSingleton<IMaintenanceNoticeCache, MaintenanceNoticeCache>();
        services.AddSingleton<IRevokedUsersCache, RevokedUsersCache>();

        return services;
    }
}
