using MediatR;

namespace Crm.Application.Dtos.User.Commands;
/// <summary>
/// Command to initiate the user invitation process by an admin.
/// </summary>
public record InviteUserCommand(
    string Email,
    IEnumerable<Guid> RoleIds) : IRequest<InvitationTokenResultDto>;
