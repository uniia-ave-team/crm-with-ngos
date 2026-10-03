using Crm.Api.Security;
using Microsoft.AspNetCore.Authorization;

namespace Crm.Api.Extensions;

/// <summary>
/// Provides extension methods for configuring custom authorization services.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Adds custom authorization handlers and policy providers to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The modified <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddCustomAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, AccessRightPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, AccessRightAuthorizationHandler>();

        return services;
    }
}
