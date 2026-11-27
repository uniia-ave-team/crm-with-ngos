using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Represents a command to validate and partially update a user's custom fields JSON payload against the global schema.
/// Only the keys present in <paramref name="Data"/> are merged onto the latest persisted values; omitted keys remain untouched.
/// </summary>
/// <param name="Data">The partial JSON object containing changed custom field values keyed by schema field ID.</param>
public record UpdateUserCustomFieldsCommand(
    JsonObject Data) : ICommand, ITransactionalCommand, ICurrentUserAware, ICurrentUserRolesAware, ISelfTargetableUserRequest
{
    /// <summary>
    /// Gets or sets the unique identifier of the target user, populated from the route parameter or request pipeline.
    /// </summary>
    [JsonIgnore]
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets a value indicating whether the command targets the currently authenticated user.
    /// </summary>
    [JsonIgnore]
    public bool TargetCurrentUser { get; init; }

    /// <summary>
    /// Gets or sets the unique identifier of the currently authenticated user, populated by the request pipeline.
    /// </summary>
    [JsonIgnore]
    public Guid CurrentUserId { get; set; }

    /// <summary>
    /// Gets or sets the role identifiers of the currently authenticated user, populated by the request pipeline.
    /// </summary>
    [JsonIgnore]
    public List<Guid> CurrentUserRoleIds { get; set; } = [];
}
