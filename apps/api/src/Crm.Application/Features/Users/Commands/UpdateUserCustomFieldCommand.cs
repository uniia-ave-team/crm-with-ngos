using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Represents a command to update the value and visibility of a specific custom field in a user's profile.
/// </summary>
/// <param name="Value">The updated string value.</param>
/// <param name="IsPublic">A value indicating whether the custom field is visible to other regular users.</param>
public record UpdateUserCustomFieldCommand(
    string Value,
    [property: JsonRequired] bool IsPublic) : ICommand, ITransactionalCommand
{
    /// <summary>
    /// Gets the unique identifier of the custom field entry to update, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid CustomFieldId { get; init; }

    /// <summary>
    /// Gets the unique identifier of the user initiating the update (used for ownership validation), populated from the route or authentication context.
    /// </summary>
    [JsonIgnore]
    public Guid UserId { get; init; }
}
