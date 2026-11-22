using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Represents a command to add a new custom key-value field to a user's profile.
/// </summary>
/// <param name="Key">The unique key or name of the custom field.</param>
/// <param name="Value">The string value of the custom field.</param>
/// <param name="IsPublic">Indicates whether the custom field is publicly visible or private.</param>
public record AddUserCustomFieldCommand(
    string Key,
    string Value,
    [property: JsonRequired] bool IsPublic) : ICommand, ITransactionalCommand
{
    /// <summary>
    /// Gets the unique identifier of the user to whom the field is added, populated from the route or authentication context.
    /// </summary>
    [JsonIgnore]
    public Guid UserId { get; init; }
}
