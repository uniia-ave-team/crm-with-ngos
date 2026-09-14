using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Microsoft.AspNetCore.Http;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Implements <see cref="ICurrentUserService"/> using <see cref="IHttpContextAccessor"/>
/// to retrieve user identity information from the active HTTP request context.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    /// <inheritdoc />
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
    public Guid GetUserId()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("CurrentUserService cannot be accessed outside of an HTTP context (e.g., inside a background worker or hosted service).");

        string? userIdClaim = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? httpContext.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId)
            ? throw new UnauthorizedAccessException("User ID claim is missing or invalid in the current security context.")
            : userId;
    }

    /// <inheritdoc />
    public List<Guid> GetRoleIds()
    {
        var user = httpContextAccessor.HttpContext?.User;

        if (user == null)
        {
            return [];
        }

        var roleIdClaims = user.FindAll(CustomClaimTypes.RoleId);
        var roleIds = new List<Guid>();

        foreach (var claim in roleIdClaims)
        {
            if (Guid.TryParse(claim.Value, out var roleId))
            {
                roleIds.Add(roleId);
            }
        }

        return roleIds;
    }
}
