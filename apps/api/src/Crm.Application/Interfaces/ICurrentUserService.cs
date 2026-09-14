using System.Security.Claims;

namespace Crm.Application.Interfaces;

/// <summary>
/// Provides access to the identity context and claims of the currently authenticated user.
/// Decouples the application layer from direct dependencies on the HTTP infrastructure.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets a value indicating whether the current request is executed by an authenticated user.
    /// </summary>
    /// <value>
    /// <c>true</c> if the user identity is authenticated; otherwise, <c>false</c>.
    /// </value>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the claims principal representing the currently authenticated user.
    /// </summary>
    /// <value>
    /// A <see cref="ClaimsPrincipal"/> instance containing all user claims if available; otherwise, <c>null</c>.
    /// </value>
    ClaimsPrincipal? User { get; }

    /// <summary>
    /// Retrieves the unique identifier of the currently authenticated user from the token claims.
    /// </summary>
    /// <returns>A non-nullable <see cref="Guid"/> representing the user ID.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown if the user is not authenticated or the ID is missing/invalid.</exception>
    Guid GetUserId();

    /// <summary>
    /// Retrieves the collection of Role IDs assigned to the currently authenticated user from their token claims.
    /// </summary>
    /// <returns>A list of <see cref="Guid"/> representing the role IDs; or an empty list if none are found.</returns>
    List<Guid> GetRoleIds();
}
