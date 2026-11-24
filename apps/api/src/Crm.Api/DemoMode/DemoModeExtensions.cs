using System.ComponentModel.DataAnnotations;
using Crm.Api.DemoMode.DemoData;
using Crm.Api.DemoMode.Sandbox;
using Crm.Application.Interfaces;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Services.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Crm.Api.DemoMode;

/// <summary>
/// Wires demo mode into the application. With <c>DemoMode:Enabled = false</c> (the default)
/// only the "demo is off" info provider is registered and nothing else changes.
/// </summary>
public static class DemoModeExtensions
{
    /// <summary>
    /// Registers the demo mode services. Must be called after the infrastructure services,
    /// because it reconfigures the database context and decorates infrastructure services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddDemoMode(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(DemoModeOptions.Position);

        services.AddOptions<DemoModeOptions>()
            .Bind(section)
            .PostConfigure(options => options.NormalizePaths())
            .ValidateDataAnnotations()
            .Validate(
                options => options.DemoBlockedPaths.All(DemoModeOptions.IsValidPathPrefix),
                $"DemoMode:DemoBlockedPaths entries must start with '/', contain at least {DemoModeOptions.MinimumPathSegmentCount} path segments, and must not contain '?' or '#'.")
            .Validate(
                options => IsValid(options.Sandbox) && options.Accounts.All(IsValid),
                "DemoMode:Sandbox or DemoMode:Accounts contains invalid values.")
            .ValidateOnStart();

        services.TryAddSingleton<IDemoModeInfoProvider, DemoModeInfoProvider>();

        if (!section.GetValue<bool>(nameof(DemoModeOptions.Enabled)))
        {
            return services;
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDemoModeState, DemoModeState>();
        services.TryAddSingleton<IDemoSandboxAccessor, DemoSandboxAccessor>();
        services.TryAddSingleton<IDemoSandboxDatabase, PostgresDemoSandboxDatabase>();
        services.TryAddSingleton<DemoSandboxManager>();
        services.TryAddSingleton<DemoDbWriteGuardInterceptor>();
        services.TryAddScoped<DemoDataSeeder>();
        services.AddHostedService<DemoSandboxCleanupService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAccessTokenClaimsProvider, DemoSandboxClaimsProvider>());

        // Options are built per scope, so every request connects to its own sandbox database.
        services.ConfigureDbContext<ApplicationDbContext>((sp, options) =>
        {
            var sandbox = sp.GetRequiredService<IDemoSandboxAccessor>().Current;

            if (sandbox is not null)
            {
                options.UseNpgsql(sandbox.ConnectionString);
            }

            options.AddInterceptors(sp.GetRequiredService<DemoDbWriteGuardInterceptor>());
        });

        services.Decorate<IRolePermissionsCache>((sp, inner) =>
            new DemoSandboxRolePermissionsCache(
                inner,
                sp.GetRequiredService<IDemoSandboxAccessor>(),
                sp.GetRequiredService<IHttpContextAccessor>()));

        // Deactivating or deleting a user revokes their tokens only inside the visitor's own sandbox.
        services.Decorate<IRevokedUsersCache>((sp, inner) =>
            new DemoSandboxRevokedUsersCache(inner, sp.GetRequiredService<IDemoSandboxAccessor>()));

        services.TryAddScoped<DemoSandboxJwtBearerEvents>();
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            if (options.EventsType != typeof(JwtBearerEventsHandler))
            {
                throw new InvalidOperationException(
                    $"Demo Mode wraps {nameof(JwtBearerEventsHandler)}, but the JWT bearer events are {options.EventsType?.FullName ?? "not set"}.");
            }

            options.EventsType = typeof(DemoSandboxJwtBearerEvents);
        });

        services.Decorate<IMaintenanceNoticeCache>((_, inner) => new DemoSharedMaintenanceNoticeCache(inner));

        // The audit log is written in the background, outside any sandbox, so it would keep every visitor's
        // changes in the shared database after their sandbox is gone.
        services.PostConfigure<AuditOptions>(options => options.Enabled = false);

        services.Decorate<IFileStorageService>((sp, inner) =>
            new DemoSandboxFileStorageService(
                inner,
                sp.GetRequiredService<IDemoSandboxAccessor>(),
                sp.GetRequiredService<IOptions<DemoModeOptions>>(),
                sp.GetRequiredService<ILogger<DemoSandboxFileStorageService>>()));

        return services;
    }

    /// <summary>
    /// Adds the demo mode middleware. Must be placed after authentication and before authorization.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    public static WebApplication UseDemoMode(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Services.GetService<IDemoModeState>()?.IsActive == true)
        {
            app.UseMiddleware<DemoModeMiddleware>();
        }

        return app;
    }

    /// <summary>
    /// Fills the database with demo data (when it is empty) and builds the template every sandbox is copied from.
    /// Must be called at startup after migrations and the regular seeders. Does nothing when demo mode is off.
    /// </summary>
    /// <param name="serviceProvider">The application services.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when demo mode is ready to serve visitors.</returns>
    public static async Task InitializeDemoModeAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (serviceProvider.GetService<IDemoModeState>()?.IsActive != true)
        {
            return;
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<IOptions<DemoModeOptions>>().Value;
            var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();

            await seeder.SeedAsync(options.GetEffectiveAccounts(), cancellationToken);

            // The seeder may have added roles; the shared permission cache must know them.
            await scope.ServiceProvider.GetRequiredService<IRolePermissionsCache>().SeedAllRolesCacheAsync(cancellationToken);
        }

        await serviceProvider.GetRequiredService<DemoSandboxManager>().InitializeAsync(cancellationToken);
    }

    private static bool IsValid(object instance) =>
        Validator.TryValidateObject(instance, new ValidationContext(instance), validationResults: null, validateAllProperties: true);

    private static void Decorate<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService, TService> decoratorFactory)
        where TService : class
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService))
            ?? throw new InvalidOperationException($"Demo Mode cannot decorate {typeof(TService).FullName}: the service is not registered.");

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
        else if (descriptor.ImplementationType is { } implementationType)
        {
            services.TryAdd(new ServiceDescriptor(implementationType, implementationType, descriptor.Lifetime));
            innerResolver = sp => (TService)sp.GetRequiredService(implementationType);
        }
        else
        {
            throw new InvalidOperationException($"Demo Mode cannot decorate {typeof(TService).FullName}.");
        }

        services.Add(new ServiceDescriptor(
            typeof(TService),
            sp => decoratorFactory(sp, innerResolver(sp)),
            descriptor.Lifetime));
    }
}
