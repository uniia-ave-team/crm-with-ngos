using System.Security.Claims;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.Users;
using Crm.Application.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="CompleteRegistrationCommand"/> to register a new user based on an invitation token.
/// Extracts secured claims from the token and links the user to the active NGO instance.
/// </summary>
public sealed partial class CompleteRegistrationCommandHandler(
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

        var principal = await ValidateTokenAsync(command.Token, cancellationToken);
        var email = ExtractEmail(principal);
        var roleIds = ExtractRoleIds(principal);

        await userRepository.EnsureDoesNotExistAsync(email, cancellationToken);

        var ngoId = await ngoRepository.GetIdAsync(cancellationToken);

        var ngoClaim = principal.FindFirst(CustomClaimTypes.NgoId)?.Value;
        if (ngoClaim is not null && (!Guid.TryParse(ngoClaim, out var tokenNgoId) || tokenNgoId != ngoId))
        {
            throw new DomainValidationException("Invitation token NGO identifier does not match the active organization.");
        }

        var roleNames = await roleRepository.GetRoleNamesByIdsAsync(roleIds, cancellationToken);
        var schemaFields = await userCustomFieldRepository.GetAllOrderedAsync(cancellationToken);

        var userBasic = await CreateUserAsync(command, email, ngoId, schemaFields, cancellationToken);

        await identityService.AddToRolesAsync(userBasic.Id, roleNames, cancellationToken);

        var authTokens = await GenerateAndSaveTokensAsync(userBasic, roleIds, cancellationToken);

        string maskedEmailForLog = email.MaskEmail();

        LogUserRegisteredSuccessfully(logger, maskedEmailForLog, userBasic.Id, roleNames);

        return authTokens;
    }

    private async Task<ClaimsPrincipal> ValidateTokenAsync(string token, CancellationToken cancellationToken)
    {
        return await tokenService.ValidateInvitationTokenAsync(token, cancellationToken)
            ?? throw new DomainValidationException("Invalid or expired invitation token.");
    }

    private static string ExtractEmail(ClaimsPrincipal principal)
    {
        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        return !string.IsNullOrWhiteSpace(email)
            ? email
            : throw new DomainValidationException("Email claim is missing from the token.");
    }

    private static List<Guid> ExtractRoleIds(ClaimsPrincipal principal)
    {
        var roleClaims = principal.FindAll(CustomClaimTypes.RoleId).ToList();
        if (roleClaims.Count == 0)
        {
            throw new DomainValidationException("Role claims are missing from the token.");
        }

        var roleIds = new List<Guid>(roleClaims.Count);

        foreach (var value in roleClaims.Select(claim => claim.Value))
        {
            if (!Guid.TryParse(value, out var roleId) || roleId == Guid.Empty)
            {
                throw new DomainValidationException($"Invalid role claim value: '{value}'.");
            }

            roleIds.Add(roleId);
        }

        return roleIds;
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
