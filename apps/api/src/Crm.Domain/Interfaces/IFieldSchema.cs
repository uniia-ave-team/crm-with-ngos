using Crm.Domain.Enums;

namespace Crm.Domain.Interfaces;

/// <summary>
/// Defines the core structural schema of a custom field.
/// </summary>
public interface IFieldSchema
{
    /// <summary>
    /// Gets the human-readable display name of the field.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the unique dictionary key used in JSON record payloads.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Gets the stored data type classification of the field.
    /// </summary>
    FieldDataType DataType { get; }

    /// <summary>
    /// Gets a value indicating whether this field requires a value upon record creation.
    /// </summary>
    bool IsRequired { get; }

    /// <summary>
    /// Gets the display order index.
    /// </summary>
    byte Order { get; }
}
