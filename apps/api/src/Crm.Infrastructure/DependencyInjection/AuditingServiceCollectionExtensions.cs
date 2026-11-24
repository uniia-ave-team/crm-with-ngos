using Crm.Infrastructure.Auditing;
using Crm.Infrastructure.BackgroundServices;
using Crm.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.DependencyInjection;

/// <summary>
/// Provides centralized dependency injection registration for the attribute-driven entity audit log pipeline.
/// To migrate audit storage from PostgreSQL to a NoSQL database (MongoDB, OpenSearch, ClickHouse, DynamoDB),
/// implement <see cref="IAuditLogSink"/> and replace the single <see cref="PostgresAuditLogSink"/> registration below.
/// </summary>
public static class AuditingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the entity change auditing interceptor, non-blocking channel queue, background batch writer,
    /// storage sink, and retention cleanup worker.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddEntityAuditing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AuditOptions>()
            .Bind(configuration.GetSection(AuditOptions.Position))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAuditActorAccessor, HttpContextAuditActorAccessor>();
        services.AddSingleton<ChannelAuditLogBuffer>();
        services.AddSingleton<IAuditLogBuffer>(sp => sp.GetRequiredService<ChannelAuditLogBuffer>());
        services.AddSingleton<EntityAuditSaveChangesInterceptor>();

        // Single swap point for migrating to NoSQL (e.g., MongoAuditLogSink, OpenSearchAuditLogSink):
        services.AddSingleton<IAuditLogSink, PostgresAuditLogSink>();

        services.AddHostedService<AuditLogBackgroundWriter>();
        services.AddHostedService<AuditLogsCleanupService>();

        return services;
    }
}
