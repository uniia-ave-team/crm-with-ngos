namespace Crm.Domain.Constants.Validation;

/// <summary>
/// Provides validation and configuration constants for <see cref="Entities.User"/> business profile entities.
/// </summary>
public static class UserValidationConstants
{
    /// <summary>
    /// The maximum allowed length for a user's first name.
    /// </summary>
    public const int MaxFirstNameLength = 100;

    /// <summary>
    /// The maximum allowed length for a user's last name.
    /// </summary>
    public const int MaxLastNameLength = 100;

    /// <summary>
    /// The maximum allowed length for a user's patronymic.
    /// </summary>
    public const int MaxPatronymicLength = 100;

    /// <summary>
    /// The maximum allowed length for a user's phone number.
    /// </summary>
    public const int MaxPhoneNumberLength = 30;

    /// <summary>
    /// The maximum allowed length for a user's country.
    /// </summary>
    public const int MaxCountryLength = 100;

    /// <summary>
    /// The maximum allowed length for a user's emergency contact details.
    /// </summary>
    public const int MaxEmergencyContactLength = 250;

    /// <summary>
    /// The maximum allowed length for a user's preferred language code.
    /// </summary>
    public const int MaxPreferredLanguageLength = 10;

    /// <summary>
    /// The maximum allowed length for a user's free-text pronouns.
    /// </summary>
    public const int MaxPronounsLength = 50;

    /// <summary>
    /// The maximum allowed length for a user's avatar URL or storage path.
    /// </summary>
    public const int MaxAvatarUrlLength = 500;
}
