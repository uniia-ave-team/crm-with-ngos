using Asp.Versioning;
using Crm.Api.Consts;
using Crm.Api.Security;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Commands;
using Crm.Application.Features.Users.Queries;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for managing user invitations and onboarding lifecycle.
/// </summary>
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route(ApiRouteConstants.DefaultControllerRoute)]
public sealed class InvitationsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retrieves a paginated, filtered, and sorted list of active (unexpired) user invitation tokens.
    /// </summary>
    /// <param name="query">Query parameters for search filtering, sorting, and pagination.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated list of active invitations.</returns>
    [HttpGet]
    [HasAccessRight(AccessRight.ManageUserActionTokens)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<ActiveInvitationDto>>> GetActiveInvitations(
        [FromQuery] GetActiveInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Generates a short opaque invitation token for a new user.
    /// </summary>
    /// <param name="command">The email and role identifiers for the invited user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The generated invitation token and its expiration timestamp.</returns>
    [HttpPost]
    [HasAccessRight(AccessRight.CreateUser)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TokenResultDto>> InviteUser(
        [FromBody] InviteUserCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Permanently revokes an active invitation token by its identifier so the invitation link can no longer be used.
    /// </summary>
    /// <param name="id">The unique identifier of the invitation token to revoke.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpDelete("{id:guid}")]
    [HasAccessRight(AccessRight.ManageUserActionTokens)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeInvitation(
        Guid id,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new RevokeActionTokenCommand(id, ActionTokenType.Invitation), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Validates an invitation token and returns its underlying details.
    /// </summary>
    /// <param name="query">The query containing the invitation token.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The extracted details from the valid invitation token.</returns>
    [HttpGet("validate")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvitationDetailsDto>> ValidateInvitation(
        [FromQuery] ValidateInvitationTokenQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Completes the user registration and onboarding process using a valid single-use invitation token.
    /// </summary>
    /// <param name="command">The registration details and invitation token.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A DTO containing the JWT access token and refresh token.</returns>
    [HttpPost("accept")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthTokensDto>> AcceptInvitation(
        [FromBody] CompleteRegistrationCommand command,
        CancellationToken cancellationToken)
    {
        var authTokens = await mediator.Send(command, cancellationToken);
        return Ok(authTokens);
    }
}
