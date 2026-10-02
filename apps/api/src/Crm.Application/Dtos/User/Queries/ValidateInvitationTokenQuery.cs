using MediatR;

namespace Crm.Application.Dtos.User.Queries;
/// <summary>
/// Query to validate an invitation token and extract its underlying claims.
/// </summary>
public record ValidateInvitationTokenQuery(string Token) : IRequest<InvitationDetailsDto>;
