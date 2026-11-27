using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Represents a command to activate a previously deactivated user.
/// This restores the domain profile to an active state and unlocks the identity account.
/// </summary>
/// <param name="UserId">The unique identifier of the user to activate.</param>
public record ActivateUserCommand(Guid UserId) : ICommand, ITransactionalCommand, ICurrentUserAware
{
    /// <summary>
    /// Gets or sets the unique identifier of the user performing the action, populated by the request pipeline.
    /// </summary>
    [JsonIgnore]
    public Guid CurrentUserId { get; set; }
}
