namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when an attempt is made to remove or demote the last remaining manager of a project.
/// </summary>
/// <param name="projectId">The unique identifier of the project.</param>
public class CannotRemoveLastProjectManagerException(Guid projectId)
    : DomainValidationException($"Cannot remove or demote the last remaining manager of project '{projectId}'.");
