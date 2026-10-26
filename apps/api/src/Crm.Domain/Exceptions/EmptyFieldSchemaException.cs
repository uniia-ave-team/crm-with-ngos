namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents an exception that is thrown when a field schema collection is null or empty.
/// </summary>
public sealed class EmptyFieldSchemaException() : DomainValidationException("Field schema must contain at least one field.");
