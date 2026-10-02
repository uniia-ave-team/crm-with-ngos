namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an entity is not found in the data store.
/// </summary>
public sealed class EntityNotFoundException(string entityType, object? key = null)
    : Exception(FormatMessage(entityType, key))
{
    private static string FormatMessage(string entityType, object? key) =>
        key is null
            ? $"Entity '{entityType}' was not found."
            : $"Entity '{entityType}' with key '{key}' was not found.";
}
