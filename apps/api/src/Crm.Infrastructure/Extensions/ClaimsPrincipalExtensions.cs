using System.Security.Claims;
using Crm.Domain.Constants;

namespace Crm.Infrastructure.Extensions;

/// <summary>
/// Provides helper extension methods for extracting strongly-typed identity claims from <see cref="ClaimsPrincipal"/>.
/// </summary>
internal static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Attempts to parse the authenticated user's <see cref="Guid"/> identifier from either
    /// <see cref="ClaimTypes.NameIdentifier"/> or <see cref="CustomClaimTypes.Sub"/>.
    /// </summary>
    /// <param name="principal">The claims principal to inspect.</param>
    /// <param name="userId">When successful, receives the parsed user identifier.</param>
    /// <returns><see langword="true"/> if a valid user identifier claim is present; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetUserId(this ClaimsPrincipal? principal, out Guid userId)
    {
        if (principal is null)
        {
            userId = Guid.Empty;
            return false;
        }

        string? userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? principal.FindFirstValue(CustomClaimTypes.Sub);

        return Guid.TryParse(userIdClaim, out userId);
    }
}
