using Crm.Domain.Entities;

namespace Crm.Domain.Consts.Entities.ValidationConstants;

/// <summary>
/// Provides validation and configuration constants for the <see cref="TemplateField"/> entity.
/// </summary>
public static class TemplateFieldValidationConstants
{
    public const int MaxNameLength = 255;
    public const int MaxKeyLength = 100;
    public const int MaxDataTypeLength = 50;
}
