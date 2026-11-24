namespace Crm.Domain.Constants.Validation;

/// <summary>
/// Provides validation and configuration constants for <see cref="Entities.UserCustomField"/> entities.
/// </summary>
public static class UserCustomFieldValidationConstants
{
    /// <summary>
    /// The maximum allowed length for a user custom field key.
    /// </summary>
    public const int MaxKeyLength = 100;

    /// <summary>
    /// The maximum allowed length for a user custom field value.
    /// </summary>
    public const int MaxValueLength = 500;
}
