using Crm.Domain.Enums;
using Crm.Domain.Interfaces;

namespace Crm.Application.Dtos.CustomTemplates;

/// <summary>
/// Represents a DTO for updating a field definition inside an existing table template blueprint.
/// </summary>
/// <param name="Id">The unique identifier of the template field (null if adding a new field during template update).</param>
/// <param name="Name">The updated display name of the template field.</param>
/// <param name="Key">The unique dictionary key used for internal mapping.</param>
/// <param name="DataType">The designated data type specifying validation rules and UI rendering.</param>
/// <param name="IsRequired">Indicates whether filling this field is mandatory.</param>
/// <param name="Order">The display order or position sequence of the field.</param>
public record UpdateTemplateFieldDto(
    Guid? Id,
    string Name,
    string Key,
    FieldDataType DataType,
    bool IsRequired,
    byte Order) : IIdentifiableField;
