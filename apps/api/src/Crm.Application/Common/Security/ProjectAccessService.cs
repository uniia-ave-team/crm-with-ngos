using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;

namespace Crm.Application.Common.Security;

/// <summary>
/// Scoped resolver that loads and caches <see cref="ProjectAccess"/> metadata per project and user,
/// avoiding mutable ambient state and duplicate database queries within a single request.
/// </summary>
/// <param name="projectRepository">The repository for loading project access metadata.</param>
/// <param name="currentUserService">The service for resolving the active user's identifier.</param>
public sealed class ProjectAccessService(
    IProjectRepository projectRepository,
    ICurrentUserService currentUserService) : IProjectAccessService
{
    private readonly Dictionary<(Guid ProjectId, Guid UserId), ProjectAccess> _cache = [];

    /// <inheritdoc />
    public async Task<ProjectAccess> GetRequiredAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        Guid userId = currentUserService.GetUserId();
        var key = (projectId, userId);

        if (_cache.TryGetValue(key, out ProjectAccess? cached))
        {
            return cached;
        }

        ProjectAccess access = await projectRepository.GetAccessAsync(projectId, userId, cancellationToken)
            ?? throw new EntityNotFoundException<Project>(projectId);

        _cache[key] = access;
        return access;
    }
}
