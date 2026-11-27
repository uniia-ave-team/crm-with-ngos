using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Auth;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Command to update the password for an existing user.
/// Returns a fresh token pair for the session that changed the password, because the change revokes
/// every refresh token of the user.
/// </summary>
/// <param name="CurrentPassword">The current password of the user.</param>
/// <param name="NewPassword">The new password to set.</param>
public record UpdatePasswordCommand(
    string CurrentPassword,
    string NewPassword) : ICommand<AuthTokensDto>, ITransactionalCommand, ICurrentUserAware
{
    /// <summary>
    /// Gets or sets the unique identifier of the user whose password is being updated, populated by the request pipeline.
    /// </summary>
    [JsonIgnore]
    public Guid UserId { get; set; }

    /// <inheritdoc />
    Guid ICurrentUserAware.CurrentUserId
    {
        get => UserId;
        set => UserId = value;
    }
}
