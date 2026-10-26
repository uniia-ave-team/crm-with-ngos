namespace Crm.Domain.Enums;

/// <summary>
/// Represents the supported data types for dynamic fields in custom table templates.
/// </summary>
public enum FieldDataType
{
    /// <summary>
    /// Standard short text input.
    /// </summary>
    Text = 0,

    /// <summary>
    /// Any numeric value (integer or floating-point).
    /// </summary>
    Number = 1,

    /// <summary>
    /// True/false boolean value (Checkbox/Toggle).
    /// </summary>
    Boolean = 2,

    /// <summary>
    /// Date only (e.g., Birthdays), ignoring timezones.
    /// </summary>
    Date = 3,

    /// <summary>
    /// Full date and time.
    /// </summary>
    DateTime = 4,

    /// <summary>
    /// Email address with standard validation.
    /// </summary>
    Email = 5,

    /// <summary>
    /// Phone number with format masking.
    /// </summary>
    PhoneNumber = 6,

    /// <summary>
    /// A URL or web link.
    /// </summary>
    Url = 7,
}
