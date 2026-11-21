namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents an exception thrown when an operation conflicts with the current state of a resource.
/// </summary>
/// <param name="message">The error message that explains the conflict.</param>
/// <param name="kind">The category of the conflict.</param>
/// <param name="innerException">The optional underlying exception that caused this conflict.</param>
public class ConflictException(
    string message,
    ConflictKind kind = ConflictKind.General,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>
    /// Gets the category of the conflict.
    /// </summary>
    public ConflictKind Kind { get; } = kind;
}
