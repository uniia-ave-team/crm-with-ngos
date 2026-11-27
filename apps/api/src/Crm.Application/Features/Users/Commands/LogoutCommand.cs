using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Represents a command to log out a user by invalidating their current refresh token.
/// </summary>
/// <param name="RefreshToken">The refresh token to invalidate.</param>
public record LogoutCommand(string RefreshToken) : ICommand, ITransactionalCommand, ICurrentUserAware
{
    /// <summary>
    /// Gets or sets the unique identifier of the user to log out, populated from the authentication context.
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
