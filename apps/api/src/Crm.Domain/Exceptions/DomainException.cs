namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents the base exception for all domain-specific errors.
/// Inheriting classes must define their own HTTP status code to support
/// polymorphic error handling in the API layer (adhering to the Open-Closed Principle).
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DomainException"/> class with a specified error message.
/// </remarks>
/// <param name="message">The message that describes the error.</param>
public abstract class DomainException(string message) : Exception(message)
{
    /// <summary>
    /// Gets the HTTP status code associated with this domain exception
    /// (e.g., 404 for NotFound, 409 for Conflict, 400 for BadRequest).
    /// </summary>
    public abstract int StatusCode { get; }
}
