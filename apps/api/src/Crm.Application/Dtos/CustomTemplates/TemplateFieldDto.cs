using Crm.Domain.Enums;

namespace Crm.Application.Dtos.CustomTemplates;

/// <summary>
/// Represents a detailed DTO of a predefined field blueprint within a table template.
/// </summary>
/// <param name="Id">The unique identifier of the template field.</param>
/// <param name="Name">The human-readable display name of the template field.</param>
/// <param name="Key">The unique key used for JSON mapping upon table instantiation.</param>
/// <param name="DataType">The data type specifying validation rules and UI rendering.</param>
/// <param name="IsRequired">Indicates whether filling this field is mandatory.</param>
/// <param name="Order">The display order or position sequence of the field.</param>
public record TemplateFieldDto(
    Guid Id,
    string Name,
    string Key,
    FieldDataType DataType,
    bool IsRequired,
    byte Order);
