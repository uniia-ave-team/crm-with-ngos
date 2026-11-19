using Crm.Domain.Common;
using Crm.Domain.Exceptions;

namespace Crm.Application.Interfaces;

/// <summary>
/// Resolves and caches <see cref="ProjectAccess"/> metadata for the current scoped request and user.
/// </summary>
public interface IProjectAccessService
{
    /// <summary>
    /// Retrieves the <see cref="ProjectAccess"/> metadata for <paramref name="projectId"/> and the current user,
    /// caching the result within the active DI scope.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved <see cref="ProjectAccess"/> instance.</returns>
    /// <exception cref="EntityNotFoundException{T}">Thrown if the project does not exist.</exception>
    Task<ProjectAccess> GetRequiredAsync(Guid projectId, CancellationToken cancellationToken = default);
}
