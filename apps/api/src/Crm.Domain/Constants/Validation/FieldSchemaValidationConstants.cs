using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Constants.Validation;

/// <summary>
/// Provides shared validation and column-length constants for <see cref="IFieldDefinition"/> types
/// (<see cref="Entities.CustomField"/>, <see cref="Entities.UserCustomField"/>, and <see cref="ValueObjects.TemplateFieldDefinition"/>).
/// </summary>
public static class FieldSchemaValidationConstants
{
    /// <summary>
    /// The maximum allowed length for a field display name.
    /// </summary>
    public const int MaxNameLength = 255;
}
