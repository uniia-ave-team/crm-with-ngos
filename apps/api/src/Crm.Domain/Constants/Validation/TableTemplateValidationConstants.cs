using Crm.Domain.Entities;

namespace Crm.Domain.Constants.Validation;

/// <summary>
/// Provides validation and configuration constants for the <see cref="TableTemplate"/> entity.
/// </summary>
public static class TableTemplateValidationConstants
{
    /// <summary>
    /// The maximum allowed length for a table template name.
    /// </summary>
    public const int MaxNameLength = 255;

    /// <summary>
    /// The maximum allowed length for a table template description.
    /// </summary>
    public const int MaxDescriptionLength = 1000;
}
