using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="InviteUserCommand"/> to generate an invitation token for a new user.
/// </summary>
/// <param name="userManager">The ASP.NET Core Identity user manager used to check existing emails.</param>
/// <param name="ngoRepository">The repository used to fetch the current NGO instance ID.</param>
/// <param name="roleRepository">The repository used to validate role existence.</param>
/// <param name="tokenService">The service responsible for generating JWT tokens.</param>
/// <param name="logger">The logger used to record the invitation process.</param>
public partial class InviteUserCommandHandler(
    UserManager<AuthUser> userManager,
    INgoRepository ngoRepository,
    IAuthRoleRepository roleRepository,
    ITokenService tokenService,
    ILogger<InviteUserCommandHandler> logger) : IRequestHandler<InviteUserCommand, InvitationTokenResultDto>
{
    /// <summary>
    /// Executes the user invitation process asynchronously.
    /// </summary>
    /// <param name="request">The command containing the email and role IDs for the invite.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A task returning the generated JWT invitation token.</returns>
    /// <exception cref="EntitiesNotFoundException">Thrown when any requested role is not found.</exception>
    /// <exception cref="EntityNotFoundException">Thrown when the NGO is not found.</exception>
    /// <exception cref="EntityAlreadyExistsException">Thrown when a user with the specified email already exists.</exception>
    public async Task<InvitationTokenResultDto> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        LogInvitingUser(logger, request.Email, request.RoleIds);

        var existingUser = await userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            LogUserAlreadyExists(logger, request.Email);
            throw new EntityAlreadyExistsException(nameof(AuthUser), request.Email);
        }

        var ngo = await ngoRepository.GetAsync(cancellationToken);

        await roleRepository.EnsureAllExistAsync(request.RoleIds, cancellationToken);

        var generateInvitationDto = new GenerateInvitationDto(
            request.Email,
            ngo.Id,
            request.RoleIds);

        var invitationToken = tokenService.GenerateInvitationToken(generateInvitationDto);

        LogUserInvitedSuccessfully(logger, request.Email);

        return invitationToken;
    }

    [LoggerMessage(EventId = LogEventIds.InvitingUser, Level = LogLevel.Information, Message = "Initiating invitation for user with email: {Email} and roles: {RoleIds}")]
    private static partial void LogInvitingUser(ILogger logger, string email, IEnumerable<Guid> roleIds);

    [LoggerMessage(EventId = LogEventIds.InviteUserCommandHandlerUserAlreadyExists, Level = LogLevel.Warning, Message = "Invitation failed. User with email '{Email}' already exists.")]
    private static partial void LogUserAlreadyExists(ILogger logger, string email);

    [LoggerMessage(EventId = LogEventIds.UserInvitedSuccessfully, Level = LogLevel.Information, Message = "Successfully generated invitation token for user: {Email}")]
    private static partial void LogUserInvitedSuccessfully(ILogger logger, string email);
}
