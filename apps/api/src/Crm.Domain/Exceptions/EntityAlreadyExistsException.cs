using Crm.Domain.Enums;

namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an entity already exists in the data store.
/// </summary>
/// <param name="entityType">The logical type name of the conflicting entity.</param>
/// <param name="key">The optional conflicting key value.</param>
public sealed class EntityAlreadyExistsException(string entityType, object? key = null)
    : ConflictException(
        key is null
            ? $"Entity '{entityType}' already exists."
            : $"Entity '{entityType}' with key '{key}' already exists.",
        ConflictKind.UniqueViolation);
