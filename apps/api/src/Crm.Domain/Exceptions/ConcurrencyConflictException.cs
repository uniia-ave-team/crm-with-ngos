namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents an exception thrown when a database operation fails due to a concurrent modification or deletion.
/// </summary>
/// <param name="message">The error message that explains the concurrency conflict.</param>
/// <param name="innerException">The optional underlying exception that caused this conflict.</param>
public sealed class ConcurrencyConflictException(
    string message,
    Exception? innerException = null)
    : ConflictException(message, ConflictKind.Concurrency, innerException);
