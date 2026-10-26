namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when field schema definitions contain duplicate key values.
/// </summary>
public class DuplicateFieldKeyException()
    : DomainValidationException("Table template fields cannot contain duplicate Key values.");
