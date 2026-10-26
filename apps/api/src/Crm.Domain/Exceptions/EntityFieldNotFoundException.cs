namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents an exception that is thrown when a specific field or property of an entity is missing, null, or empty.
/// </summary>
public sealed class EntityFieldNotFoundException<T>(string fieldName)
    : NotFoundException($"Field '{fieldName}' for entity '{typeof(T).Name}' was not found or is empty.");
