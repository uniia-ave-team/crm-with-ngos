namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an entity of type <typeparamref name="T"/> is not found in the data store.
/// </summary>
/// <typeparam name="T">The type of the entity.</typeparam>
/// <param name="key">The optional identifier of the missing entity.</param>
public sealed class EntityNotFoundException<T>(object? key = null)
    : NotFoundException(
        key is null
            ? $"Entity '{typeof(T).Name}' was not found."
            : $"Entity '{typeof(T).Name}' with key '{key}' was not found.");
