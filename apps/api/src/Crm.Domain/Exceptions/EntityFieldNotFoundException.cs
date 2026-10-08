namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents an exception that is thrown when a specific field or property of an entity is missing, null, or empty.
/// </summary>
public sealed class EntityFieldNotFoundException(string entityType, string fieldName)
    : Exception($"Field '{fieldName}' for entity '{entityType}' was not found or is empty.");
