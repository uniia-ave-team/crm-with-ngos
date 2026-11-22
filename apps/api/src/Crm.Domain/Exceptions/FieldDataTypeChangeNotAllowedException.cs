namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents an exception that is thrown when attempting to modify a field's data type on a table that already contains records.
/// </summary>
/// <param name="fieldKey">The key of the field whose data type cannot be changed.</param>
public sealed class FieldDataTypeChangeNotAllowedException(string fieldKey)
    : DomainValidationException($"Changing data type for field '{fieldKey}' is not allowed because the table already contains records.");
