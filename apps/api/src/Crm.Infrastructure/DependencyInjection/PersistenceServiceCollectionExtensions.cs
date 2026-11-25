using System.Reflection;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Auditing;
using Crm.Infrastructure.Consts;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Interceptors;
using Crm.Infrastructure.Persistence.Seeders;
using Crm.Infrastructure.Persistence.Seeders.SystemPresets;
using Crm.Infrastructure.Repositories;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Infrastructure.DependencyInjection;

/// <summary>
/// Provides dependency injection registration for Entity Framework Core persistence,
/// interceptors, unit of work, database seeders, and repositories.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF Core <see cref="ApplicationDbContext"/>, ordered database interceptors,
    /// <see cref="IUnitOfWork"/>, database seeders, and system table template presets.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IDbExceptionTranslator, PostgresDbExceptionTranslator>();
        services.AddSingleton<RowLockCommandInterceptor>();
        services.AddSingleton<DbExceptionTranslationInterceptor>();
        services.AddSingleton(sp => new EntityAuditMetadataSaveChangesInterceptor(
            sp.GetRequiredService<TimeProvider>(),
            sp.GetService<IAuditActorAccessor>()));

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            // Registration order is deliberate:
            // 1. RowLockCommandInterceptor rewrites tagged SELECT queries to FOR NO KEY UPDATE.
            // 2. EntityAuditMetadataSaveChangesInterceptor populates CreatedAt/UpdatedAt/CreatedBy/UpdatedBy.
            // 3. EntityAuditSaveChangesInterceptor (when registered) captures diffs after metadata is populated
            //    and cleans up pending audit state on SaveChangesFailed before any translated exception is thrown.
            // 4. DbExceptionTranslationInterceptor runs LAST so SaveChangesFailed hooks above finish cleanly.
            var interceptors = new List<IInterceptor>(4)
            {
                sp.GetRequiredService<RowLockCommandInterceptor>(),
                sp.GetRequiredService<EntityAuditMetadataSaveChangesInterceptor>(),
            };

            if (sp.GetService<EntityAuditSaveChangesInterceptor>() is { } auditInterceptor)
            {
                interceptors.Add(auditInterceptor);
            }

            interceptors.Add(sp.GetRequiredService<DbExceptionTranslationInterceptor>());

            options.UseNpgsql(configuration.GetConnectionString(ConfigurationKeys.PostgreSqlConnection))
                .WithExpressionExpanding()
                .AddInterceptors(interceptors);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddStrategies<IDatabaseSeeder>(typeof(PersistenceServiceCollectionExtensions).Assembly);
        services.AddStrategies<ISystemTableTemplate>(typeof(PersistenceServiceCollectionExtensions).Assembly);

        return services;
    }

    /// <summary>
    /// Registers all domain and identity repository implementations in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<INgoRepository, NgoRepository>();
        services.AddScoped<IAuthUserRepository, AuthUserRepository>();
        services.AddScoped<IAuthRoleRepository, AuthRoleRepository>();
        services.AddScoped<IUserRefreshTokenRepository, UserRefreshTokenRepository>();
        services.AddScoped<IUserActionTokenRepository, UserActionTokenRepository>();
        services.AddScoped<IUserCustomFieldRepository, UserCustomFieldRepository>();
        services.AddScoped<ILoginPageImageRepository, LoginPageImageRepository>();
        services.AddScoped<ITableTemplateRepository, TableTemplateRepository>();
        services.AddScoped<ICustomRecordRepository, CustomRecordRepository>();
        services.AddScoped<ICustomTableRepository, CustomTableRepository>();
        services.AddScoped<ICustomFieldRepository, CustomFieldRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITopicRepository, TopicRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IProjectTaskRepository, ProjectTaskRepository>();
        services.AddScoped<ITaskCommentRepository, TaskCommentRepository>();
        services.AddScoped<ITaskAttachmentRepository, TaskAttachmentRepository>();
        services.AddScoped<IMaintenanceNoticeRepository, MaintenanceNoticeRepository>();

        return services;
    }

    private static IServiceCollection AddStrategies<TStrategy>(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        var strategyType = typeof(TStrategy);

        var implementations = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => strategyType.IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });

        foreach (var implementation in implementations)
        {
            services.AddTransient(strategyType, implementation);
        }

        return services;
    }
}
