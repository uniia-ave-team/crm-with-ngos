using Crm.Application.Extensions;
using Crm.Infrastructure.BackgroundServices;
using Crm.Infrastructure.Consts;
using Crm.Infrastructure.Persistence;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Infrastructure.DependencyInjection;

/// <summary>
/// Orchestrates dependency injection registration for the entire infrastructure layer by composing
/// focused subsystem installers (persistence, auditing, identity, storage, caching, and background workers).
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers all infrastructure services, database context, auditing pipeline, repositories,
    /// identity, JWT authentication, file storage, caching, health checks, Mapster mappings, and background workers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddPersistence(configuration);
        services.AddEntityAuditing(configuration);
        services.AddRepositories();
        services.AddCustomIdentity(configuration);
        services.AddJwtAuthentication(configuration);
        services.AddIdentityApplicationServices();
        services.AddFileStorageServices(configuration);
        services.AddCaching();
        services.AddInfrastructureHealthChecks();
        services.AddMapsterMappings();
        services.AddInfrastructureBackgroundServices();

        return services;
    }

    /// <summary>
    /// Applies any pending Entity Framework Core migrations to the database upon application startup,
    /// ensuring the database schema is fully up-to-date.
    /// </summary>
    /// <param name="app">The application service provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The original <see cref="IServiceProvider"/> instance for chaining.</returns>
    public static async Task<IServiceProvider> UseInfrastructureDatabaseAsync(
        this IServiceProvider app,
        CancellationToken cancellationToken = default)
    {
        await using var scope = app.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);

        return app;
    }

    /// <summary>
    /// Scans the application and infrastructure assemblies for <see cref="IRegister"/> implementations
    /// and configures Mapster global settings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddMapsterMappings(this IServiceCollection services)
    {
        TypeAdapterConfig.GlobalSettings.Scan(
            typeof(ApplicationServiceCollectionExtensions).Assembly,
            typeof(InfrastructureServiceCollectionExtensions).Assembly);

        return services;
    }

    private static IServiceCollection AddInfrastructureHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(HealthCheckNames.DbContext);

        return services;
    }

    private static IServiceCollection AddInfrastructureBackgroundServices(this IServiceCollection services)
    {
        services.AddHostedService<ExpiredTokensCleanupService>();
        services.AddHostedService<OrphanedTempFilesCleanupService>();

        return services;
    }
}
