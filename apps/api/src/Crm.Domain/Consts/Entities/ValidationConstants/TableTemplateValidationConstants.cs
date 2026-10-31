using Crm.Domain.Entities;

namespace Crm.Domain.Consts.Entities.ValidationConstants;

/// <summary>
/// Provides validation and configuration constants for the <see cref="TableTemplate"/> entity.
/// </summary>
public static class TableTemplateValidationConstants
{
    public const int MaxNameLength = 255;
    public const int MaxDescriptionLength = 1000;
}
