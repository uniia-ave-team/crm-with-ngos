using Crm.Domain.Common;
using Crm.Domain.Exceptions;

namespace Crm.Application.Interfaces;

/// <summary>
/// Validates that target task assignee identifiers represent existing, active users
/// with view access to the parent project.
/// </summary>
public interface ITaskAssigneeValidator
{
    /// <summary>
    /// Ensures all specified assignee identifiers are existing, active users eligible for the project.
    /// </summary>
    /// <param name="assigneeIds">The target assignee identifiers to validate.</param>
    /// <param name="projectId">The unique identifier of the parent project.</param>
    /// <param name="projectAccess">The verified project access metadata.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous validation operation.</returns>
    /// <exception cref="EntityNotFoundException{T}">Thrown if any specified user does not exist.</exception>
    /// <exception cref="CannotAssignUserToTaskException">Thrown if any specified user is deactivated or lacks access to a private project.</exception>
    Task ValidateAsync(
        IReadOnlyCollection<Guid> assigneeIds,
        Guid projectId,
        ProjectAccess projectAccess,
        CancellationToken cancellationToken = default);
}
