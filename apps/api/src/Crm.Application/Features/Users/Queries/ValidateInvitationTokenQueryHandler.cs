using System.Security.Claims;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="ValidateInvitationTokenQuery"/> to parse and validate an incoming JWT invitation token.
/// </summary>
/// <param name="tokenService">The service responsible for validating JWT signatures and expiration dates.</param>
/// <param name="authUserRepository">The repository used to verify the existence of the user associated with the invitation token.</param>
/// <param name="logger">The logger used to record the validation process.</param>
public partial class ValidateInvitationTokenQueryHandler(
    ITokenService tokenService,
    IAuthUserRepository authUserRepository,
    ILogger<ValidateInvitationTokenQueryHandler> logger) : IRequestHandler<ValidateInvitationTokenQuery, InvitationDetailsDto>
{
    public async Task<InvitationDetailsDto> Handle(ValidateInvitationTokenQuery request, CancellationToken cancellationToken)
    {
        LogValidatingToken(logger);

        var principal = await tokenService.ValidateInvitationTokenAsync(request.Token)
            ?? throw new InvalidOperationException("Invalid or expired invitation token.");

        string email = principal.FindFirst(ClaimTypes.Email).Value;

        await authUserRepository.EnsureNotExistsAsync(email, cancellationToken);

        string ngoIdString = principal.FindFirst(CustomClaimTypes.NgoId).Value;

        string roleIdString = principal.FindFirst(CustomClaimTypes.RoleId).Value;

        LogTokenValidatedSuccessfully(logger, email);

        return new InvitationDetailsDto(
            email,
            Guid.Parse(ngoIdString),
            Guid.Parse(roleIdString));
    }

    [LoggerMessage(EventId = LogEventIds.ValidatingToken, Level = LogLevel.Information, Message = "Validating incoming invitation token.")]
    private static partial void LogValidatingToken(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.TokenValidatedSuccessfully, Level = LogLevel.Information, Message = "Invitation token validated successfully for email: {Email}")]
    private static partial void LogTokenValidatedSuccessfully(ILogger logger, string email);
}
