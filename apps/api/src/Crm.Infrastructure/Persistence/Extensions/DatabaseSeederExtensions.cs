using Crm.Application.Interfaces;
using Crm.Infrastructure.Persistence.Seeders;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Provides extension methods for application initialization and database seeding.
/// </summary>
public static class DatabaseSeederExtensions
{
    /// <summary>
    /// Creates an isolated service scope from the specified service provider to safely resolve
    /// and execute application database seeders asynchronously.
    /// </summary>
    /// <param name="serviceProvider">The application service provider instance used to create a dependency injection scope.</param>
    /// <returns>A task that represents the asynchronous database seeding operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if required services (such as <see cref="AdminRoleSeeder"/>) are not registered in the dependency injection container.</exception>
    public static async Task SeedDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        var seeders = services.GetServices<IDatabaseSeeder>();

        foreach (var seeder in seeders)
        {
            await seeder.SeedAsync(cancellationToken);
        }

        var permissionsCache = services.GetRequiredService<IRolePermissionsCache>();
        await permissionsCache.SeedAllRolesCacheAsync(cancellationToken);
    }
}
