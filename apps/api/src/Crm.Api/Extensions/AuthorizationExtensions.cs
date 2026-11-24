using Crm.Api.Security;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace Crm.Api.Extensions;

/// <summary>
/// Provides extension methods for configuring custom authorization services and access-right policies.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Registers an authorization policy for every <see cref="AccessRight"/> value at startup
    /// and binds <see cref="AccessRightAuthorizationHandler"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The modified <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddCustomAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            foreach (var right in Enum.GetValues<AccessRight>())
            {
                options.AddPolicy(
                    right.ToClaimValue(),
                    policy => policy
                        .RequireAuthenticatedUser()
                        .AddRequirements(new AccessRightRequirement(right)));
            }
        });

        services.AddScoped<IAuthorizationHandler, AccessRightAuthorizationHandler>();

        return services;
    }
}
