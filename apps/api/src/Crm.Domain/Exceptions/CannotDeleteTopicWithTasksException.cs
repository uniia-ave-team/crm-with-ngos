namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when an attempt is made to delete a topic that still contains assigned tasks.
/// </summary>
/// <param name="topicId">The unique identifier of the topic that cannot be deleted.</param>
public sealed class CannotDeleteTopicWithTasksException(Guid topicId)
    : DomainValidationException($"Topic with ID '{topicId}' cannot be deleted because it still contains tasks.");
