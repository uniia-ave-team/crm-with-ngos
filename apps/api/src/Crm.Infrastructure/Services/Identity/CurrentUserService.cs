using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Infrastructure.Extensions;
using Microsoft.AspNetCore.Http;

namespace Crm.Infrastructure.Services.Identity;

/// <summary>
/// Implements <see cref="ICurrentUserService"/> using <see cref="IHttpContextAccessor"/>
/// to retrieve user identity information from the active HTTP request context.
/// </summary>
/// <param name="httpContextAccessor">The HTTP context accessor providing access to the current request principal.</param>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    /// <inheritdoc />
    public Guid GetUserId()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("CurrentUserService cannot be accessed outside of an HTTP context (e.g., inside a background worker or hosted service).");

        return httpContext.User.TryGetUserId(out Guid userId)
            ? userId
            : throw new UnauthorizedAccessException("User ID claim is missing or invalid in the current security context.");
    }

    /// <inheritdoc />
    public List<Guid> GetRoleIds()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return [];
        }

        var result = new List<Guid>();
        foreach (var claim in user.FindAll(CustomClaimTypes.RoleId))
        {
            if (Guid.TryParse(claim.Value, out var roleId))
            {
                result.Add(roleId);
            }
        }

        return result;
    }
}
