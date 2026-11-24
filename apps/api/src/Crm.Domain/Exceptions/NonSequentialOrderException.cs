namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when display order values are not continuous and sequential starting from 0 without gaps.
/// </summary>
/// <param name="message">The error message explaining the sequential order violation.</param>
public class NonSequentialOrderException(
    string message = "Display order values must be continuous and sequential starting from 0 without gaps.")
    : DomainValidationException(message);
