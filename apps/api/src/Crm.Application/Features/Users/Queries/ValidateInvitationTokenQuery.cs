using Crm.Application.Dtos.Users;
using Mediator;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Query to validate an invitation token and extract its underlying claims.
/// </summary>
public record ValidateInvitationTokenQuery(string Token) : IQuery<InvitationDetailsDto>;
