namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when field schema definitions contain duplicate display order values.
/// </summary>
public class DuplicateFieldOrderException()
    : DomainValidationException("Table template fields cannot contain duplicate display order (Order) values.");
