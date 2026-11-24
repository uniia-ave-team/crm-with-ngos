using Crm.Domain.Entities;

namespace Crm.Domain.Constants.Validation;

/// <summary>
/// Provides validation and configuration constants for the <see cref="CustomTable"/> entity.
/// </summary>
public static class CustomTableValidationConstants
{
    /// <summary>
    /// The maximum allowed length for a custom table name.
    /// </summary>
    public const int MaxNameLength = 255;

    /// <summary>
    /// The maximum allowed length for a custom table description.
    /// </summary>
    public const int MaxDescriptionLength = 1000;
}
