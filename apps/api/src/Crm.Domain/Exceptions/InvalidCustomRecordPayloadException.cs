namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when a custom record JSON payload violates the table's field schema or data type rules.
/// </summary>
/// <param name="message">The error message describing the schema or value validation failure.</param>
public sealed class InvalidCustomRecordPayloadException(string message)
    : DomainValidationException(message);
