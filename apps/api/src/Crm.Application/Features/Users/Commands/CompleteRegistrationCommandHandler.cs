using System.Security.Claims;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="CompleteRegistrationCommand"/> to register a new user based on an invitation token.
/// Extracts secured claims from the token and links the user to the active NGO instance.
/// </summary>
public partial class CompleteRegistrationCommandHandler(
    ITokenService tokenService,
    INgoRepository ngoRepository,
    UserManager<AuthUser> userManager,
    IAuthRoleRepository roleRepository,
    ILogger<CompleteRegistrationCommandHandler> logger) : IRequestHandler<CompleteRegistrationCommand, AuthTokensDto>
{
    public async Task<AuthTokensDto> Handle(CompleteRegistrationCommand request, CancellationToken cancellationToken)
    {
        LogCompletingRegistration(logger);

        var principal = await ValidateTokenAsync(request.Token);
        var email = ExtractEmail(principal);
        var roleIds = ExtractRoleIds(principal);

        await EnsureUserDoesNotExistAsync(email);

        var ngo = await ngoRepository.GetAsync(cancellationToken);
        var roleNames = await roleRepository.GetRoleNamesByIdsAsync(roleIds, cancellationToken);

        var authUser = await CreateUserAsync(request, email, ngo.Id);
        await AssignRolesAsync(authUser, email, roleNames);

        var authTokens = await GenerateAndSaveTokensAsync(authUser, roleIds);

        LogUserRegisteredSuccessfully(logger, email, authUser.Id, roleNames);

        return authTokens;
    }

    private async Task<ClaimsPrincipal> ValidateTokenAsync(string token)
    {
        return await tokenService.ValidateInvitationTokenAsync(token)
            ?? throw new InvalidOperationException("Invalid or expired invitation token.");
    }

    private static string ExtractEmail(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Email)
            ?? throw new InvalidOperationException("Email claim is missing from the token.");
    }

    private static List<Guid> ExtractRoleIds(ClaimsPrincipal principal)
    {
        var roleClaims = principal.FindAll(CustomClaimTypes.RoleId).ToList();

        return roleClaims.Count == 0
            ? throw new InvalidOperationException("Role claims are missing from the token.")
            : [.. roleClaims.Select(c => Guid.Parse(c.Value))];
    }

    private async Task EnsureUserDoesNotExistAsync(string email)
    {
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            LogUserAlreadyExists(logger, email);
            throw new EntityAlreadyExistsException(nameof(AuthUser), email);
        }
    }

    private async Task<AuthUser> CreateUserAsync(CompleteRegistrationCommand request, string email, Guid ngoId)
    {
        var authUser = new AuthUser
        {
            UserName = email,
            Email = email,
            UserProfile = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                NgoId = ngoId,
                IsActive = true,
            },
        };

        var result = await userManager.CreateAsync(authUser, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
            LogUserRegistrationFailed(logger, email, errors);
            throw new InvalidOperationException($"User registration failed: {errors}");
        }

        return authUser;
    }

    private async Task AssignRolesAsync(AuthUser user, string email, List<string> roleNames)
    {
        var result = await userManager.AddToRolesAsync(user, roleNames);
        if (!result.Succeeded)
        {
            var roleErrors = string.Join(" | ", result.Errors.Select(e => e.Description));
            LogRolesAssignmentFailed(logger, email, roleNames, roleErrors);
            throw new InvalidOperationException($"Failed to assign roles: {roleErrors}");
        }
    }

    private async Task<AuthTokensDto> GenerateAndSaveTokensAsync(AuthUser user, List<Guid> roleIds)
    {
        var accessToken = tokenService.GenerateAccessToken(user, roleIds);
        var refreshToken = tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken.Token;
        user.RefreshTokenExpiryTime = refreshToken.ExpiresAt;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join(" | ", updateResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to save refresh token for user '{user.Email}': {errors}");
        }

        return new AuthTokensDto(accessToken.Token, refreshToken.Token);
    }

    [LoggerMessage(EventId = LogEventIds.CompletingRegistration, Level = LogLevel.Information, Message = "Initiating registration completion from invitation token.")]
    private static partial void LogCompletingRegistration(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.UserAlreadyExists, Level = LogLevel.Warning, Message = "Failed to complete registration for user '{Email}'. User already exists.")]
    private static partial void LogUserAlreadyExists(ILogger logger, string email);

    [LoggerMessage(EventId = LogEventIds.UserRegistrationFailed, Level = LogLevel.Warning, Message = "Failed to complete registration for user '{Email}'. Reason: {Errors}")]
    private static partial void LogUserRegistrationFailed(ILogger logger, string email, string errors);

    [LoggerMessage(EventId = LogEventIds.RolesAssignmentFailed, Level = LogLevel.Warning, Message = "Failed to assign roles '{RoleNames}' to user '{Email}'. Reason: {Errors}")]
    private static partial void LogRolesAssignmentFailed(ILogger logger, string email, IEnumerable<string> roleNames, string errors);

    [LoggerMessage(EventId = LogEventIds.UserRegisteredSuccessfully, Level = LogLevel.Information, Message = "User '{Email}' successfully registered with ID: {UserId} and assigned roles: {RoleNames}")]
    private static partial void LogUserRegisteredSuccessfully(ILogger logger, string email, Guid userId, IEnumerable<string> roleNames);
}
