using System.Security.Claims;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Users;
using Crm.Application.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="ValidateInvitationTokenQuery"/> to parse and validate an incoming JWT invitation token.
/// </summary>
/// <param name="tokenService">The service responsible for validating JWT signatures and expiration dates.</param>
/// <param name="authUserRepository">The repository used to verify the existence of the user associated with the invitation token.</param>
/// <param name="logger">The logger used to record the validation process.</param>
public sealed partial class ValidateInvitationTokenQueryHandler(
    ITokenService tokenService,
    IAuthUserRepository authUserRepository,
    ILogger<ValidateInvitationTokenQueryHandler> logger) : IQueryHandler<ValidateInvitationTokenQuery, InvitationDetailsDto>
{
    /// <inheritdoc />
    public async ValueTask<InvitationDetailsDto> Handle(ValidateInvitationTokenQuery query, CancellationToken cancellationToken)
    {
        LogValidatingToken(logger);

        var principal = await tokenService.ValidateInvitationTokenAsync(query.Token, cancellationToken)
            ?? throw new DomainValidationException("Invalid or expired invitation token.");

        string? email = principal.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainValidationException("Invitation token is missing the email claim.");
        }

        await authUserRepository.EnsureDoesNotExistAsync(email, cancellationToken);

        string? ngoIdString = principal.FindFirst(CustomClaimTypes.NgoId)?.Value;
        if (string.IsNullOrWhiteSpace(ngoIdString) || !Guid.TryParse(ngoIdString, out Guid ngoId) || ngoId == Guid.Empty)
        {
            throw new DomainValidationException("Invitation token is missing the NGO identifier claim.");
        }

        var roleClaims = principal.FindAll(CustomClaimTypes.RoleId).ToList();
        if (roleClaims.Count == 0)
        {
            throw new DomainValidationException("Invitation token is missing the role identifier claim.");
        }

        var roleIds = new List<Guid>(roleClaims.Count);
        foreach (var claim in roleClaims)
        {
            if (!Guid.TryParse(claim.Value, out Guid roleId) || roleId == Guid.Empty)
            {
                throw new DomainValidationException($"Invalid role claim value: '{claim.Value}'.");
            }

            roleIds.Add(roleId);
        }

        string maskedEmailForLog = email.MaskEmail();

        LogTokenValidatedSuccessfully(logger, maskedEmailForLog);

        return new InvitationDetailsDto(
            email,
            ngoId,
            roleIds);
    }

    [LoggerMessage(EventId = LogEventIds.ValidatingToken, Level = LogLevel.Information, Message = "Validating incoming invitation token.")]
    private static partial void LogValidatingToken(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.TokenValidatedSuccessfully, Level = LogLevel.Information, Message = "Invitation token validated successfully for email: {Email}")]
    private static partial void LogTokenValidatedSuccessfully(ILogger logger, string email);
}
