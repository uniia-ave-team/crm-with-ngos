namespace Crm.Domain.Constants.Validation;

/// <summary>
/// Provides validation and configuration constants for <see cref="Entities.Ngo"/> entities.
/// </summary>
public static class NgoValidationConstants
{
    /// <summary>
    /// The maximum allowed length for an NGO name.
    /// </summary>
    public const int MaxNameLength = 200;

    /// <summary>
    /// The maximum allowed length for an NGO logo URL or storage path.
    /// </summary>
    public const int MaxLogoUrlLength = 500;
}
