using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.Users;
using Crm.Application.Extensions;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="CompleteRegistrationCommand"/> to register a new user based on a persisted single-use invitation token.
/// </summary>
/// <param name="actionTokenService">The service used to validate, consume, and revoke invitation tokens.</param>
/// <param name="tokenService">The service responsible for issuing authentication JWT and refresh tokens.</param>
/// <param name="ngoRepository">The repository used to verify the active NGO instance.</param>
/// <param name="userRepository">The repository used to ensure email uniqueness.</param>
/// <param name="roleRepository">The repository used to resolve role names from role identifiers.</param>
/// <param name="userCustomFieldRepository">The repository used to build default custom field payloads.</param>
/// <param name="identityService">The identity service used to create the user and assign roles.</param>
/// <param name="timeProvider">The time provider supplying UTC timestamps.</param>
/// <param name="logger">The logger used to record the registration process.</param>
public sealed partial class CompleteRegistrationCommandHandler(
    IUserActionTokenService actionTokenService,
    ITokenService tokenService,
    INgoRepository ngoRepository,
    IAuthUserRepository userRepository,
    IAuthRoleRepository roleRepository,
    IUserCustomFieldRepository userCustomFieldRepository,
    IIdentityService identityService,
    TimeProvider timeProvider,
    ILogger<CompleteRegistrationCommandHandler> logger) : ICommandHandler<CompleteRegistrationCommand, AuthTokensDto>
{
    /// <inheritdoc />
    public async ValueTask<AuthTokensDto> Handle(CompleteRegistrationCommand command, CancellationToken cancellationToken)
    {
        LogCompletingRegistration(logger);

        var resolved = await actionTokenService.ResolveValidInvitationAsync(command.Token, cancellationToken);

        string email = resolved.Token.TargetEmail;
        List<Guid> roleIds = [.. resolved.Metadata.RoleIds];

        await userRepository.EnsureDoesNotExistAsync(email, cancellationToken);

        Guid ngoId = await ngoRepository.GetIdAsync(cancellationToken);

        if (resolved.Metadata.NgoId != ngoId)
        {
            throw new DomainValidationException("Invitation token NGO identifier does not match the active organization.");
        }

        if (!await actionTokenService.TryConsumeAsync(resolved.Token.Id, ActionTokenType.Invitation, cancellationToken))
        {
            throw new DomainValidationException("Invalid or expired invitation token.");
        }

        var roleNames = await roleRepository.GetRoleNamesByIdsAsync(roleIds, cancellationToken);
        var schemaFields = await userCustomFieldRepository.GetAllOrderedAsync(cancellationToken);

        var userBasic = await CreateUserAsync(command, email, ngoId, schemaFields, cancellationToken);

        await identityService.AddToRolesAsync(userBasic.Id, roleNames, cancellationToken);

        var authTokens = await GenerateAndSaveTokensAsync(userBasic, roleIds, cancellationToken);

        await actionTokenService.RevokeAllByTargetEmailAsync(email, ActionTokenType.Invitation, cancellationToken);

        string maskedEmail = email.MaskEmail();

        LogUserRegisteredSuccessfully(logger, maskedEmail, userBasic.Id, roleNames);

        return authTokens;
    }

    private async Task<UserBasicDto> CreateUserAsync(
        CompleteRegistrationCommand command,
        string email,
        Guid ngoId,
        IReadOnlyList<UserCustomField> schemaFields,
        CancellationToken cancellationToken = default)
    {
        var user = command.Adapt<User>();

        user.NgoId = ngoId;
        user.IsActive = true;
        user.CreatedAt = timeProvider.GetUtcNow().UtcDateTime;
        user.CustomFields = schemaFields.CreateDefaultPayload();

        return await identityService.CreateUserAsync(email, command.Password, user, cancellationToken);
    }

    private async Task<AuthTokensDto> GenerateAndSaveTokensAsync(UserBasicDto user, List<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        var accessToken = tokenService.GenerateAccessToken(user, roleIds);
        var refreshToken = tokenService.GenerateRefreshToken();

        await identityService.AddRefreshTokenAsync(user.Id, refreshToken, cancellationToken);

        return new AuthTokensDto(accessToken.Token, refreshToken.Token);
    }

    [LoggerMessage(EventId = LogEventIds.CompletingRegistration, Level = LogLevel.Information, Message = "Initiating registration completion from invitation token.")]
    private static partial void LogCompletingRegistration(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.UserRegisteredSuccessfully, Level = LogLevel.Information, Message = "User '{Email}' successfully registered with ID: {UserId} and assigned roles: {RoleNames}")]
    private static partial void LogUserRegisteredSuccessfully(ILogger logger, string email, Guid userId, IEnumerable<string> roleNames);
}
