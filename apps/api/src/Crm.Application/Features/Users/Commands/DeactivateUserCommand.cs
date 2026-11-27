using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Represents a command to deactivate a user in the system.
/// This performs a soft-delete on the domain profile and locks the identity account.
/// </summary>
/// <param name="UserId">The unique identifier of the user to deactivate.</param>
public record DeactivateUserCommand(Guid UserId) : ICommand, ITransactionalCommand, ICurrentUserAware
{
    /// <summary>
    /// Gets or sets the unique identifier of the user performing the action, populated by the request pipeline.
    /// </summary>
    [JsonIgnore]
    public Guid CurrentUserId { get; set; }
}
