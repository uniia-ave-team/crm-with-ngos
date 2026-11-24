namespace Crm.Application.Interfaces;

/// <summary>
/// Provides access to the identity context and claims of the currently authenticated user.
/// Decouples the application layer from direct dependencies on the HTTP infrastructure.
/// </summary>
public interface ICurrentUserService
{
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
