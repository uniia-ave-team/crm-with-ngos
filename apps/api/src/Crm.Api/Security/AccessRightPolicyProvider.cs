using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Crm.Api.Security;

/// <summary>
/// Provides authorization policies dynamically based on access right claim values.
/// </summary>
/// <param name="options">The authorization options.</param>
public class AccessRightPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    private static readonly Dictionary<string, AccessRight> PolicyMap = Enum
        .GetValues<AccessRight>()
        .ToDictionary(right => right.ToClaimValue(), right => right, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a specific authorization policy by name.
    /// </summary>
    /// <param name="policyName">The name of the authorization policy.</param>
    /// <returns>An <see cref="AuthorizationPolicy"/> if matched by an access right; otherwise, falls back to the default implementation.</returns>
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (PolicyMap.TryGetValue(policyName, out var right))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new AccessRightRequirement(right))
                .Build();

            return policy;
        }

        return await base.GetPolicyAsync(policyName);
    }
}
