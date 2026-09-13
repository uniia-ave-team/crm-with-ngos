namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an entity already exists in the data store.
/// </summary>
public sealed class EntityAlreadyExistsException(string entityType, object? key = null)
    : DomainException(FormatMessage(entityType, key))
{
    public override int StatusCode => 409;

    private static string FormatMessage(string entityType, object? key) =>
        key is null
            ? $"Entity '{entityType}' already exists."
            : $"Entity '{entityType}' with key '{key}' already exists.";
}
