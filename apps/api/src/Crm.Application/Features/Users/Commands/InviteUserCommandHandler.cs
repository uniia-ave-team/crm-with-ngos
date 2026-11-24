using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Users;
using Crm.Application.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="InviteUserCommand"/> to generate an invitation token for a new user.
/// </summary>
/// <param name="userRepository">The repository used to check if a user already exists.</param>
/// <param name="ngoRepository">The repository used to fetch the current NGO instance ID.</param>
/// <param name="roleRepository">The repository used to validate role existence.</param>
/// <param name="tokenService">The service responsible for generating JWT tokens.</param>
/// <param name="logger">The logger used to record the invitation process.</param>
public sealed partial class InviteUserCommandHandler(
    IAuthUserRepository userRepository,
    INgoRepository ngoRepository,
    IAuthRoleRepository roleRepository,
    ITokenService tokenService,
    ILogger<InviteUserCommandHandler> logger) : ICommandHandler<InviteUserCommand, TokenResultDto>
{
    /// <summary>
    /// Executes the user invitation process asynchronously.
    /// </summary>
    /// <param name="command">The command containing the email and role IDs for the invite.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task returning the generated JWT invitation token.</returns>
    /// <exception cref="EntitiesNotFoundException">Thrown when any requested role is not found.</exception>
    /// <exception cref="NotFoundException">Thrown when the NGO is not found.</exception>
    /// <exception cref="EntityAlreadyExistsException">Thrown when a user with the specified email already exists.</exception>
    public async ValueTask<TokenResultDto> Handle(InviteUserCommand command, CancellationToken cancellationToken)
    {
        var maskedEmail = command.Email.MaskEmail();

        LogInvitingUser(logger, maskedEmail, command.RoleIds);

        await userRepository.EnsureDoesNotExistAsync(command.Email, cancellationToken);

        var ngoId = await ngoRepository.GetIdAsync(cancellationToken);

        await roleRepository.EnsureAllExistAsync(command.RoleIds, cancellationToken);

        var generateInvitationDto = new GenerateInvitationDto(
            command.Email,
            ngoId,
            command.RoleIds);

        var invitationToken = tokenService.GenerateInvitationToken(generateInvitationDto);

        LogUserInvitedSuccessfully(logger, maskedEmail);

        return invitationToken;
    }

    [LoggerMessage(EventId = LogEventIds.InvitingUser, Level = LogLevel.Information, Message = "Initiating invitation for user with email: {Email} and roles: {RoleIds}")]
    private static partial void LogInvitingUser(ILogger logger, string email, IEnumerable<Guid> roleIds);

    [LoggerMessage(EventId = LogEventIds.UserInvitedSuccessfully, Level = LogLevel.Information, Message = "Successfully generated invitation token for user: {Email}")]
    private static partial void LogUserInvitedSuccessfully(ILogger logger, string email);
}
