using System.Security.Claims;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="CompleteRegistrationCommand"/> to register a new user based on an invitation token.
/// Extracts secured claims from the token and links the user to the active NGO instance.
/// </summary>
public partial class CompleteRegistrationCommandHandler(
    ITokenService tokenService,
    INgoRepository ngoRepository,
    IAuthUserRepository userRepository,
    IAuthRoleRepository roleRepository,
    IIdentityService identityService,
    TimeProvider timeProvider,
    ILogger<CompleteRegistrationCommandHandler> logger) : IRequestHandler<CompleteRegistrationCommand, AuthTokensDto>
{
    public async Task<AuthTokensDto> Handle(CompleteRegistrationCommand request, CancellationToken cancellationToken)
    {
        LogCompletingRegistration(logger);

        var principal = await ValidateTokenAsync(request.Token);
        var email = ExtractEmail(principal);
        var roleIds = ExtractRoleIds(principal);

        await userRepository.EnsureNotExistsAsync(email, cancellationToken);

        var ngo = await ngoRepository.GetAsync(cancellationToken);
        var roleNames = await roleRepository.GetRoleNamesByIdsAsync(roleIds, cancellationToken);

        var userBasic = await CreateUserAsync(request, email, ngo.Id, cancellationToken);

        await identityService.AddToRolesAsync(userBasic.Id, roleNames, cancellationToken);

        var authTokens = await GenerateAndSaveTokensAsync(userBasic, roleIds, cancellationToken);

        LogUserRegisteredSuccessfully(logger, email, userBasic.Id, roleNames);

        return authTokens;
    }

    private async Task<ClaimsPrincipal> ValidateTokenAsync(string token)
    {
        return await tokenService.ValidateInvitationTokenAsync(token)
            ?? throw new InvalidOperationException("Invalid or expired invitation token.");
    }

    private static string ExtractEmail(ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Email)?.Value
            ?? throw new InvalidOperationException("Email claim is missing from the token.");
    }

    private static List<Guid> ExtractRoleIds(ClaimsPrincipal principal)
    {
        var roleClaims = principal.FindAll(CustomClaimTypes.RoleId).ToList();

        var roleIds = new List<Guid>(roleClaims.Count);

        foreach (var value in roleClaims.Select(claim => claim.Value))
        {
            if (!Guid.TryParse(value, out var roleId))
            {
                throw new InvalidOperationException($"Invalid role claim value: '{value}'.");
            }

            roleIds.Add(roleId);
        }

        return roleIds;
    }

    private async Task<UserBasicDto> CreateUserAsync(CompleteRegistrationCommand request, string email, Guid ngoId, CancellationToken cancellationToken = default)
    {
        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            NgoId = ngoId,
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        return await identityService.CreateUserAsync(email, request.Password, user, cancellationToken);
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
