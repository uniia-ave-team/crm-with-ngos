using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Constants.Validation;

/// <summary>
/// Provides shared validation and column-length constants for <see cref="IFieldSchema"/> entities
/// (<see cref="Entities.CustomField"/> and <see cref="Entities.TemplateField"/>).
/// </summary>
public static class FieldSchemaValidationConstants
{
    /// <summary>
    /// The maximum allowed length for a field display name.
    /// </summary>
    public const int MaxNameLength = 255;

    /// <summary>
    /// The maximum allowed length for a machine-readable field key.
    /// </summary>
    public const int MaxKeyLength = 100;

    /// <summary>
    /// The maximum allowed length for the serialized data type name.
    /// </summary>
    public const int MaxDataTypeLength = 50;
}
