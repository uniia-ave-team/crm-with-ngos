namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when field schema definitions contain non-sequential display order values.
/// </summary>
public sealed class NonSequentialFieldOrderException()
    : NonSequentialOrderException("Field schema display order values must be continuous and sequential without gaps.");
