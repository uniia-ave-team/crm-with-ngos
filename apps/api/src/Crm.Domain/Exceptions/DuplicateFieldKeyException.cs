namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when field schema definitions contain duplicate key values.
/// </summary>
public sealed class DuplicateFieldKeyException()
    : DomainValidationException("Field schema cannot contain duplicate Key values.");
