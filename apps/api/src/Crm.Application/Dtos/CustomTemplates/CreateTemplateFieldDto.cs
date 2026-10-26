using Crm.Domain.Enums;
using Crm.Domain.Interfaces;

namespace Crm.Application.Dtos.CustomTemplates;

/// <summary>
/// Represents a DTO for defining a new field blueprint within a table template.
/// </summary>
/// <param name="Name">The human-readable display name of the template field.</param>
/// <param name="Key">The unique key used for internal mapping when copying to custom fields.</param>
/// <param name="DataType">The data type specifying validation rules and UI rendering.</param>
/// <param name="IsRequired">Indicates whether filling this field is mandatory.</param>
/// <param name="Order">The display order or position sequence of the field.</param>
public record CreateTemplateFieldDto(
    string Name,
    string Key,
    FieldDataType DataType,
    bool IsRequired,
    byte Order) : IFieldSchema;
