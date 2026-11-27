using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Command to permanently delete a user and all their personal data (GDPR hard-delete).
/// Acts as the DTO for the deletion request.
/// </summary>
/// <param name="UserId">The unique identifier of the user to delete.</param>
/// <param name="TargetCurrentUser">Indicates whether the currently authenticated user is deleting their own account (<c>/me</c> route).</param>
public record DeleteUserCommand(
    Guid UserId = default,
    bool TargetCurrentUser = false) : ICommand, ITransactionalCommand, ICurrentUserAware, ISelfTargetableUserRequest
{
    /// <inheritdoc />
    public Guid UserId { get; set; } = UserId;

    /// <summary>
    /// Gets or sets the unique identifier of the authenticated user performing the deletion, populated by the request pipeline.
    /// When an administrator deletes another user, projects solely managed by the deleted user are transferred to this administrator;
    /// self-deletion blocks while sole manager.
    /// </summary>
    [JsonIgnore]
    public Guid? CurrentUserId { get; set; }

    /// <inheritdoc />
    Guid ICurrentUserAware.CurrentUserId
    {
        get => CurrentUserId ?? Guid.Empty;
        set => CurrentUserId = value;
    }
}
