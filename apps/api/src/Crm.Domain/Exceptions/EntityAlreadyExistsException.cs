namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an entity already exists in the data store.
/// </summary>
public sealed class EntityAlreadyExistsException(string entityType, object? key = null)
    : Exception(FormatMessage(entityType, key))
{
    private static string FormatMessage(string entityType, object? key) =>
        key is null
            ? $"Entity '{entityType}' already exists."
            : $"Entity '{entityType}' with key '{key}' already exists.";
}
