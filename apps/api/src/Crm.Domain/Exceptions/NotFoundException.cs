namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents a base exception thrown when an entity is not found in the data store.
/// </summary>
public abstract class NotFoundException(string message) : Exception(message);
