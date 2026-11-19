using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;

namespace Crm.Application.Features.Tasks.Services;

/// <summary>
/// Validates task assignee identifiers without loading full user entities.
/// </summary>
/// <param name="userRepository">The user repository for querying user activation statuses.</param>
/// <param name="projectRepository">The project repository for querying project memberships.</param>
public sealed class TaskAssigneeValidator(
    IUserRepository userRepository,
    IProjectRepository projectRepository) : ITaskAssigneeValidator
{
    /// <inheritdoc />
    public async Task ValidateAsync(
        IReadOnlyCollection<Guid> assigneeIds,
        Guid projectId,
        ProjectAccess projectAccess,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assigneeIds);
        ArgumentNullException.ThrowIfNull(projectAccess);

        List<Guid> distinctIds = [.. assigneeIds.Distinct()];
        if (distinctIds.Count == 0)
        {
            return;
        }

        Dictionary<Guid, bool> activeStatuses = await userRepository.GetActiveStatusesAsync(distinctIds, cancellationToken);
        bool isPrivate = projectAccess.Visibility == ProjectVisibility.Private;
        HashSet<Guid>? memberIds = isPrivate
            ? await projectRepository.GetMemberUserIdsAsync(projectId, distinctIds, cancellationToken)
            : null;

        foreach (Guid userId in distinctIds)
        {
            if (!activeStatuses.TryGetValue(userId, out bool isActive))
            {
                throw new EntityNotFoundException<User>(userId);
            }

            if (!isActive)
            {
                throw new CannotAssignUserToTaskException(userId, AssignmentErrorMessages.UserAccountDeactivated);
            }

            if (isPrivate && userId != projectAccess.CreatedBy && !memberIds.Contains(userId))
            {
                throw new CannotAssignUserToTaskException(userId, AssignmentErrorMessages.NoPrivateProjectAccess(projectId));
            }
        }
    }
}
