using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Crm.Api.DemoMode;

/// <summary>
/// Provides extension methods for configuring, validating, and conditionally registering isolated Demo Mode
/// decorators, interceptors, and <see cref="DemoModeMiddleware"/> at startup.
/// When <c>DemoMode:Enabled</c> is <see langword="false"/> or missing, nothing is registered in DI or the HTTP pipeline.
/// </summary>
public static class DemoModeExtensions
{
    /// <summary>
    /// Binds, normalizes, and validates <see cref="DemoModeOptions"/> when <c>DemoMode:Enabled</c> is <see langword="true"/>,
    /// and decorates core pipeline/infrastructure services with Demo Mode wrappers.
    /// When disabled, leaves the container completely untouched for zero runtime overhead.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
    /// <param name="configuration">The application <see cref="IConfiguration"/>.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddDemoMode(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(DemoModeOptions.Position);
        if (!section.GetValue<bool>(nameof(DemoModeOptions.Enabled)))
        {
            return services;
        }

        bool hasCustomExcludedPaths = section.GetSection(nameof(DemoModeOptions.ExcludedPaths)).Exists();
        bool hasCustomBlockedPaths = section.GetSection(nameof(DemoModeOptions.DemoBlockedPaths)).Exists();

        services.AddOptions<DemoModeOptions>()
            .Configure(options =>
            {
                if (hasCustomExcludedPaths)
                {
                    options.ExcludedPaths = [];
                }

                if (hasCustomBlockedPaths)
                {
                    options.DemoBlockedPaths = [];
                }
            })
            .Bind(section)
            .PostConfigure(options => options.NormalizePaths())
            .ValidateDataAnnotations()
            .Validate(
                options =>
                    options.ExcludedPaths.All(DemoModeOptions.IsValidPathPrefix) &&
                    options.DemoBlockedPaths.All(DemoModeOptions.IsValidPathPrefix),
                $"DemoMode path prefixes must start with '/', contain at least {DemoModeOptions.MinimumPathSegmentCount} path segments, and must not contain query strings or fragments ('?' or '#').")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDemoModeState, DemoModeState>();
        services.TryAddSingleton<IDemoExecutionContext, DemoExecutionContext>();
        services.TryAddSingleton<DemoRefreshTokenStore>();
        services.TryAddSingleton<DemoDryRunIdRegistry>();
        services.TryAddSingleton<DemoDbWriteGuardInterceptor>();
        services.TryAddSingleton<IDemoCommandPolicy, DemoCommandPolicy>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDemoCommandInterceptor, DemoSafeIdentityCommandInterceptor>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDemoCommandInterceptor, DemoDryRunFollowUpInterceptor>());

        if (services.Any(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)))
        {
            services.ConfigureDbContext<ApplicationDbContext>((sp, options) =>
                options.AddInterceptors(sp.GetRequiredService<DemoDbWriteGuardInterceptor>()));
        }

        services.Decorate<IMediator>((sp, inner) =>
            inner is DemoMediator
                ? inner
                : new DemoMediator(
                    inner,
                    sp.GetRequiredService<IDemoModeState>(),
                    sp.GetRequiredService<IDemoExecutionContext>(),
                    sp.GetRequiredService<IDemoCommandPolicy>(),
                    sp.GetServices<IDemoCommandInterceptor>()));

        if (services.Any(d => d.ServiceType == typeof(IMediator)))
        {
            services.Decorate<ISender>((sp, _) => sp.GetRequiredService<IMediator>());
            services.Decorate<IPublisher>((sp, _) => sp.GetRequiredService<IMediator>());
        }

        services.Decorate<IUnitOfWork>((sp, inner) =>
            inner is DemoUnitOfWork
                ? inner
                : new DemoUnitOfWork(
                    inner,
                    sp.GetRequiredService<IDemoExecutionContext>(),
                    sp.GetRequiredService<ILogger<DemoUnitOfWork>>(),
                    sp.GetRequiredService<IDemoModeState>(),
                    sp.GetService<IHttpContextAccessor>()));
        services.Decorate<IUserRefreshTokenRepository>((sp, inner) =>
            inner is DemoUserRefreshTokenRepository
                ? inner
                : new DemoUserRefreshTokenRepository(
                    inner,
                    sp.GetRequiredService<DemoRefreshTokenStore>(),
                    sp.GetRequiredService<IDemoModeState>(),
                    sp.GetRequiredService<IDemoExecutionContext>()));
        services.Decorate<IFileTransactionTracker>((sp, inner) =>
            inner is DemoFileTransactionTracker
                ? inner
                : new DemoFileTransactionTracker(
                    inner,
                    sp.GetRequiredService<IDemoExecutionContext>()));
        services.Decorate<IFileStorageService>((sp, inner) =>
            inner is DemoFileStorageService
                ? inner
                : new DemoFileStorageService(
                    inner,
                    sp.GetRequiredService<IOptions<FileStorageOptions>>(),
                    sp.GetRequiredService<IImageValidator>(),
                    sp.GetRequiredService<IDemoModeState>()));
        services.Decorate<IRolePermissionsCache>((sp, inner) =>
            inner is DemoRolePermissionsCache
                ? inner
                : new DemoRolePermissionsCache(
                    inner,
                    sp.GetRequiredService<IDemoModeState>()));

        return services;
    }

    /// <summary>
    /// Registers <see cref="DemoModeMiddleware"/> in the HTTP request pipeline only when Demo Mode was enabled at startup.
    /// When disabled, adds zero middleware overhead to the pipeline.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> pipeline builder.</param>
    /// <returns>The original <see cref="WebApplication"/> instance for method chaining.</returns>
    public static WebApplication UseDemoMode(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Services.GetService<IDemoModeState>()?.IsActive == true)
        {
            app.UseMiddleware<DemoModeMiddleware>();
        }

        return app;
    }

    private static void Decorate<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService, TService> decoratorFactory)
        where TService : class
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor is null)
        {
            return;
        }

        services.Remove(descriptor);

        Func<IServiceProvider, TService> innerResolver;
        if (descriptor.ImplementationInstance is TService instance)
        {
            innerResolver = _ => instance;
        }
        else if (descriptor.ImplementationFactory is not null)
        {
            var factory = descriptor.ImplementationFactory;
            innerResolver = sp => (TService)factory(sp);
        }
        else if (descriptor.ImplementationType is { } implType)
        {
            services.TryAdd(new ServiceDescriptor(implType, implType, descriptor.Lifetime));
            innerResolver = sp => (TService)sp.GetRequiredService(implType);
        }
        else
        {
            throw new InvalidOperationException($"Cannot decorate service {typeof(TService).FullName}.");
        }

        services.Add(new ServiceDescriptor(
            typeof(TService),
            sp => decoratorFactory(sp, innerResolver(sp)),
            descriptor.Lifetime));
    }
}
