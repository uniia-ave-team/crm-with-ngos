using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Users;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Command to initiate the user invitation process by an admin.
/// </summary>
public record InviteUserCommand(
    string Email,
    IReadOnlyList<Guid> RoleIds) : ICommand<TokenResultDto>, ITransactionalCommand;
