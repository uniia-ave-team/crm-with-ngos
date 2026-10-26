namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an entity of type <typeparamref name="T"/> is not found in the data store.
/// </summary>
/// <typeparam name="T">The type of the entity.</typeparam>
public sealed class EntityNotFoundException<T>(object? key = null) : NotFoundException(FormatMessage(key))
{
    private static string FormatMessage(object? key) =>
        key is null
            ? $"Entity '{typeof(T).Name}' was not found."
            : $"Entity '{typeof(T).Name}' with key '{key}' was not found.";
}
