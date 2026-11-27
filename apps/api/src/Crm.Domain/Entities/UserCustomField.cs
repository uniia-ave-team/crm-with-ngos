using System.Text.Json.Nodes;
using Crm.Domain.Attributes;
using Crm.Domain.Entities.Base;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a global custom field (column) definition shared across all <see cref="User"/> profiles.
/// Mirrors <see cref="CustomField"/> by inheriting from <see cref="FieldDefinitionBase"/> and adding visibility control.
/// Individual user values are stored as a JSONB <see cref="JsonObject"/> on <see cref="User.CustomFields"/>.
/// </summary>
[Auditable]
public class UserCustomField : FieldDefinitionBase
{
    /// <summary>
    /// Gets or sets a value indicating whether values for this custom field column are visible
    /// to regular users without global custom-field viewing permissions.
    /// </summary>
    public bool IsPublic { get; set; }
}
