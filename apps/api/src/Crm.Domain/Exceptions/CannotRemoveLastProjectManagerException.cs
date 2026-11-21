namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when an attempt is made to remove, demote, deactivate, or delete the last remaining active manager of a project.
/// </summary>
public class CannotRemoveLastProjectManagerException : DomainValidationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CannotRemoveLastProjectManagerException"/> class
    /// for a project member removal or demotion attempt.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    public CannotRemoveLastProjectManagerException(Guid projectId)
        : base($"Cannot remove or demote the last remaining manager of project '{projectId}'.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CannotRemoveLastProjectManagerException"/> class
    /// for a user deactivation or deletion attempt that would leave one or more projects without an active manager.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="projectIds">The identifiers of projects where the user is the sole active manager.</param>
    public CannotRemoveLastProjectManagerException(Guid userId, IReadOnlyCollection<Guid> projectIds)
        : base($"Cannot deactivate or delete user '{userId}' because they are the sole active manager of {projectIds.Count} project(s). Transfer project management before proceeding.")
    {
    }
}
