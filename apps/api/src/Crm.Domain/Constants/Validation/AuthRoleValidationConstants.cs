namespace Crm.Domain.Constants.Validation;

/// <summary>
/// Provides validation and configuration constants for AuthRole entities.
/// </summary>
public static class AuthRoleValidationConstants
{
    /// <summary>
    /// The maximum allowed length for a role name.
    /// </summary>
    public const int MaxNameLength = 256;

    /// <summary>
    /// The maximum allowed length for a role's feminitive name.
    /// </summary>
    public const int MaxFeminitiveNameLength = 256;

    /// <summary>
    /// The maximum allowed length for a role's plural name.
    /// </summary>
    public const int MaxPluralNameLength = 256;
}
