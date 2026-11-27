using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Users;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Command to initiate the user invitation process by an admin.
/// </summary>
/// <param name="Email">The email address of the user to invite.</param>
/// <param name="RoleIds">The role identifiers to assign upon registration.</param>
public record InviteUserCommand(
    string Email,
    IReadOnlyList<Guid> RoleIds) : ICommand<TokenResultDto>, ITransactionalCommand, ICurrentUserAware
{
    /// <inheritdoc />
    [JsonIgnore]
    public Guid CurrentUserId { get; set; }
}
