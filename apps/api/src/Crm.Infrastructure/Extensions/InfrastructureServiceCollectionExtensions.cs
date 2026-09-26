using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Crm.Application.Common.Consts;
using Crm.Application.Common.Options;
using Crm.Application.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.BackgroundServices;
using Crm.Infrastructure.Consts;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Seeders;
using Crm.Infrastructure.Repositories;
using Crm.Infrastructure.Services;
using Mapster;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;

namespace Crm.Infrastructure.Extensions;

/// <summary>
/// Provides extension methods for setting up infrastructure layer services in the DI container.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers all infrastructure services, database context, repositories, authentication, caching, and CORS.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The web hosting environment.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddPersistence(configuration);
        services.AddRepositories();
        services.AddCustomIdentity(configuration);
        services.AddJwtAuthentication(configuration);
        services.AddInfrastructureApplicationServices();
        services.AddCaching();
        services.AddCorsSettings(configuration, environment);
        services.AddInfrastructureHealthChecks();
        services.AddAuthorization();
        services.AddMapsterMappings();
        services.AddInfrastructureBackgroundServices();

        return services;
    }

    /// <summary>
    /// Applies any pending Entity Framework core migrations to the database upon application startup,
    /// ensuring the database schema is fully up-to-date.
    /// </summary>
    /// <param name="app">The application service provider / host.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The original <see cref="IServiceProvider"/> instance for chaining.</returns>
    public static async Task<IServiceProvider> UseInfrastructureDatabaseAsync(this IServiceProvider app, CancellationToken cancellationToken = default)
    {
        using var scope = app.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);

        return app;
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString(ConfigurationKeys.PostgreSqlConnection)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddTransient<IDatabaseSeeder, AdminRoleSeeder>();

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<INgoRepository, NgoRepository>();
        services.AddScoped<IAuthUserRepository, AuthUserRepository>();
        services.AddScoped<IAuthRoleRepository, AuthRoleRepository>();
        services.AddScoped<IUserRefreshTokenRepository, UserRefreshTokenRepository>();

        return services;
    }

    private static IServiceCollection AddCustomIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CustomIdentityOptions>()
            .Bind(configuration.GetSection(CustomIdentityOptions.Position))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddIdentity<AuthUser, AuthRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        services.AddSingleton<IConfigureOptions<IdentityOptions>>(sp =>
        {
            var identityOptions = sp.GetRequiredService<IOptions<CustomIdentityOptions>>().Value;

            return new ConfigureNamedOptions<IdentityOptions>(string.Empty, options =>
            {
                options.Password.RequireDigit = identityOptions.RequireDigit;
                options.Password.RequiredLength = identityOptions.RequiredLength;
                options.Password.RequireNonAlphanumeric = identityOptions.RequireNonAlphanumeric;
                options.Password.RequireUppercase = identityOptions.RequireUppercase;
                options.Password.RequireLowercase = identityOptions.RequireLowercase;
                options.User.RequireUniqueEmail = identityOptions.RequireUniqueEmail;
            });
        });

        return services;
    }

    private static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        services.AddOptions<JwtOptions>()
                .Bind(configuration.GetSection(JwtOptions.Position))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<IOptions<JwtOptions>>((options, jwtOpts) =>
        {
            var jwtSettings = jwtOpts.Value;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            };
        });

        return services;
    }

    private static IServiceCollection AddInfrastructureApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddHttpContextAccessor();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IRoleIdentityService, RoleIdentityService>();

        return services;
    }

    private static IServiceCollection AddCaching(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IRolePermissionsCache, RolePermissionsCache>();

        return services;
    }

    private static IServiceCollection AddCorsSettings(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddOptions<AppCorsOptions>()
                .Bind(configuration.GetSection(AppCorsOptions.Position))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddCors(options =>
        {
            options.AddPolicy(AppCorsOptions.PolicyName, policy =>
            {
                if (environment.IsDevelopment())
                {
                    policy.SetIsOriginAllowed(origin => true);
                }
                else
                {
                    var corsOptions = configuration
                        .GetSection(AppCorsOptions.Position)
                        .Get<AppCorsOptions>()
                        ?? throw new InvalidOperationException($"Configuration section '{AppCorsOptions.Position}' is missing.");

                    if (corsOptions.AllowedOrigins is null || corsOptions.AllowedOrigins.Length == 0)
                    {
                        throw new InvalidOperationException("CORS allowed origins are not configured.");
                    }

                    policy.WithOrigins(corsOptions.AllowedOrigins);
                }

                policy.WithMethods(
                          HttpMethods.Get,
                          HttpMethods.Post,
                          HttpMethods.Put,
                          HttpMethods.Delete,
                          HttpMethods.Options)
                      .WithHeaders(
                          HeaderNames.Authorization,
                          HeaderNames.ContentType,
                          CustomHttpHeaders.XRequestedWith)
                      .AllowCredentials();
            });
        });

        return services;
    }

    private static IServiceCollection AddInfrastructureHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(HealthCheckNames.DbContext);

        return services;
    }

    /// <summary>
    /// Scans the application and infrastructure assemblies for <see cref="IRegister"/> implementations
    /// and configures Mapster global settings.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for method chaining.</returns>
    private static IServiceCollection AddMapsterMappings(this IServiceCollection services)
    {
        TypeAdapterConfig.GlobalSettings.Scan(
            typeof(ApplicationServiceCollectionExtensions).Assembly,
            typeof(InfrastructureServiceCollectionExtensions).Assembly);

        return services;
    }

    private static IServiceCollection AddInfrastructureBackgroundServices(this IServiceCollection services)
    {
        services.AddHostedService<ExpiredTokensCleanupService>();

        return services;
    }
}
