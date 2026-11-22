namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when field schema definitions contain duplicate display order values.
/// </summary>
public sealed class DuplicateFieldOrderException()
    : DomainValidationException("Field schema cannot contain duplicate display order (Order) values.");
