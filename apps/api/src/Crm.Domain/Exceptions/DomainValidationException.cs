namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents errors that occur when domain validation fails.
/// </summary>
/// <param name="message">The error message that explains the reason for the exception.</param>
public abstract class DomainValidationException(string message) : Exception(message);
