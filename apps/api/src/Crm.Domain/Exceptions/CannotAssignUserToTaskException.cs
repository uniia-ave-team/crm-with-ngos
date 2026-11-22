namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when an attempt is made to assign an inactive or unauthorized user to a project task.
/// </summary>
/// <param name="userId">The unique identifier of the user who cannot be assigned.</param>
/// <param name="reason">The reason why the user cannot be assigned to the task.</param>
public sealed class CannotAssignUserToTaskException(Guid userId, string reason)
    : DomainValidationException($"User with ID '{userId}' cannot be assigned to the task: {reason}");
