namespace Crm.Domain.Consts.Entities;

/// <summary>
/// Provides validation and configuration constants for User business profile entities.
/// </summary>
public static class UserValidationConstants
{
    public const int MaxFirstNameLength = 100;
    public const int MaxLastNameLength = 100;
    public const int MaxPatronymicLength = 100;
    public const int MaxInternalPositionLength = 150;
    public const int MaxPhoneNumberLength = 30;
    public const int MaxCountryLength = 100;
    public const int MaxEmergencyContactLength = 250;
    public const int MaxPreferredLanguageLength = 10;
    public const int MaxPronounsLength = 50;
    public const int MaxAvatarUrlLength = 500;
}
