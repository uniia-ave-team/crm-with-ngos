using Crm.Application.Interfaces;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Services.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.DependencyInjection;

/// <summary>
/// Provides dependency injection registration for ASP.NET Core Identity, JWT Bearer authentication,
/// and stateless identity/authorization services.
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    /// <summary>
    /// Configures ASP.NET Core Identity, password rules, token lifespans, and identity manager wrappers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddCustomIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CustomIdentityOptions>()
            .Bind(configuration.GetSection(CustomIdentityOptions.Position))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddIdentity<AuthUser, AuthRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddOptions<DataProtectionTokenProviderOptions>()
            .Configure<IOptions<JwtOptions>>((options, jwtOpts) =>
            {
                options.TokenLifespan = TimeSpan.FromHours(jwtOpts.Value.ResetPasswordTokenExpiryHours);
            });

        services.AddOptions<IdentityOptions>()
            .Configure<IOptions<CustomIdentityOptions>>((options, customOptionsAccessor) =>
            {
                CustomIdentityOptions identityOptions = customOptionsAccessor.Value;
                options.Password.RequireDigit = identityOptions.RequireDigit;
                options.Password.RequiredLength = identityOptions.RequiredLength;
                options.Password.RequireNonAlphanumeric = identityOptions.RequireNonAlphanumeric;
                options.Password.RequireUppercase = identityOptions.RequireUppercase;
                options.Password.RequireLowercase = identityOptions.RequireLowercase;
                options.User.RequireUniqueEmail = identityOptions.RequireUniqueEmail;
            });

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IRoleIdentityService, RoleIdentityService>();

        return services;
    }

    /// <summary>
    /// Configures JWT Bearer authentication options and validates <see cref="JwtOptions"/> on startup.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Position))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<JwtBearerEventsHandler>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOpts) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = JwtTokenValidationHelper.CreateValidationParameters(
                    jwtOpts.Value,
                    validateLifetime: true);
                options.EventsType = typeof(JwtBearerEventsHandler);
            });

        return services;
    }

    /// <summary>
    /// Registers stateless identity, token, permission, and current-user services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddIdentityApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IActionTokenProtector, AesGcmActionTokenProtector>();
        services.AddScoped<IUserActionTokenService, UserActionTokenService>();
        services.AddSingleton<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IPermissionService, PermissionService>();
        services.AddSingleton<IOptionService, OptionService>();

        return services;
    }
}
