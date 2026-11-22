namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when an entity's end date is earlier than its start date.
/// </summary>
public sealed class InvalidDateRangeException()
    : DomainValidationException(DefaultMessage)
{
    /// <summary>
    /// The canonical error message for an inverted date range.
    /// </summary>
    public const string DefaultMessage = "End date cannot be earlier than start date.";
}
