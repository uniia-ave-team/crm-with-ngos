namespace Crm.Domain.Exceptions;

/// <summary>
/// Categorizes the underlying cause of a resource conflict.
/// </summary>
public enum ConflictKind
{
    /// <summary>
    /// A general resource state conflict.
    /// </summary>
    General = 0,

    /// <summary>
    /// A duplicate value violated a uniqueness constraint.
    /// </summary>
    UniqueViolation = 1,

    /// <summary>
    /// A referenced or dependent resource constraint was violated.
    /// </summary>
    ReferenceViolation = 2,

    /// <summary>
    /// The resource was modified or deleted concurrently.
    /// </summary>
    Concurrency = 3,
}
