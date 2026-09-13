using System.Security.Claims;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="ValidateInvitationTokenQuery"/> to parse and validate an incoming JWT invitation token.
/// </summary>
/// <param name="tokenService">The service responsible for validating JWT signatures and expiration dates.</param>
/// <param name="userManager">The ASP.NET Core Identity user manager used to check if the user already exists.</param>
/// <param name="logger">The logger used to record the validation process.</param>
public partial class ValidateInvitationTokenQueryHandler(
    ITokenService tokenService,
    UserManager<AuthUser> userManager,
    ILogger<ValidateInvitationTokenQueryHandler> logger) : IRequestHandler<ValidateInvitationTokenQuery, InvitationDetailsDto>
{
    public async Task<InvitationDetailsDto> Handle(ValidateInvitationTokenQuery request, CancellationToken cancellationToken)
    {
        LogValidatingToken(logger);

        var principal = await tokenService.ValidateInvitationTokenAsync(request.Token)
            ?? throw new InvalidOperationException("Invalid or expired invitation token.");

        string email = principal.FindFirstValue(ClaimTypes.Email)
            ?? throw new InvalidOperationException("Email claim is missing from the token.");

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            LogUserAlreadyRegistered(logger, email);
            throw new EntityAlreadyExistsException(nameof(AuthUser), email);
        }

        string ngoIdString = principal.FindFirstValue(CustomClaimTypes.NgoId)
            ?? throw new InvalidOperationException("NGO ID claim is missing from the token.");

        string roleIdString = principal.FindFirstValue(CustomClaimTypes.RoleId)
            ?? throw new InvalidOperationException("Role ID claim is missing from the token.");

        LogTokenValidatedSuccessfully(logger, email);

        return new InvitationDetailsDto(
            email,
            Guid.Parse(ngoIdString),
            Guid.Parse(roleIdString));
    }

    [LoggerMessage(EventId = LogEventIds.ValidatingToken, Level = LogLevel.Information, Message = "Validating incoming invitation token.")]
    private static partial void LogValidatingToken(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.UserAlreadyRegistered, Level = LogLevel.Warning, Message = "Token validation failed. User with email '{Email}' is already registered.")]
    private static partial void LogUserAlreadyRegistered(ILogger logger, string email);

    [LoggerMessage(EventId = LogEventIds.TokenValidatedSuccessfully, Level = LogLevel.Information, Message = "Invitation token validated successfully for email: {Email}")]
    private static partial void LogTokenValidatedSuccessfully(ILogger logger, string email);
}
