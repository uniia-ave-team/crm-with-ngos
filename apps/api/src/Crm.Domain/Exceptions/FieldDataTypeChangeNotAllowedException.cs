namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents an exception that is thrown when attempting to modify a field's data type on a table that already contains records.
/// </summary>
public sealed class FieldDataTypeChangeNotAllowedException(string fieldKey)
    : DomainValidationException(FormatMessage(fieldKey))
{
    private static string FormatMessage(string fieldKey) =>
        $"Changing data type for field '{fieldKey}' is not allowed because the table already contains records.";
}
