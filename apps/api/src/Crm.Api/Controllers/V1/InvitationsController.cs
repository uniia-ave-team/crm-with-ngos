using Asp.Versioning;
using Crm.Api.Security;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Dtos.User.Queries;
using Crm.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for managing user invitations and onboarding lifecycle.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class InvitationsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Generates an invitation link/token for a new user.
    /// </summary>
    /// <param name="command">The email and role for the invited user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The generated invitation token.</returns>
    [HttpPost]
    [HasAccessRight(AccessRight.CreateUser)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InvitationTokenResultDto>> InviteUserAsync(
        [FromBody] InviteUserCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Validates an invitation token and returns its underlying details.
    /// </summary>
    /// <param name="query">The query containing the invitation token.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The extracted details from the valid invitation token.</returns>
    [HttpGet("validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InvitationDetailsDto>> ValidateInvitationAsync(
        [FromQuery] ValidateInvitationTokenQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Completes the user registration and onboarding process using a valid invitation token.
    /// </summary>
    /// <param name="command">The registration details and invitation token.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A DTO containing the JWT access token and refresh token.</returns>
    [HttpPost("accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthTokensDto>> AcceptInvitationAsync(
        [FromBody] CompleteRegistrationCommand command,
        CancellationToken cancellationToken)
    {
        var authTokens = await mediator.Send(command, cancellationToken);
        return Ok(authTokens);
    }
}
