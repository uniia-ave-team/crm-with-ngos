using Asp.Versioning;
using Crm.Api.Consts;
using Crm.Api.DemoMode;
using Crm.Application.Dtos.Auth;
using Crm.Application.Features.Users.Commands;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for authentication and token management processes.
/// </summary>
/// <param name="mediator">The mediator instance used for dispatching commands.</param>
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route(ApiRouteConstants.DefaultControllerRoute)]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Authenticates a user and issues access and refresh tokens.
    /// </summary>
    /// <param name="command">The authentication credentials.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A DTO containing the JWT access token and refresh token.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [DemoSessionEndpoint(DemoSessionEndpointKind.SignIn)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthTokensDto>> Login(
        [FromBody] AuthenticateUserCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// </summary>
    /// <param name="command">The current access and refresh tokens.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A new pair of access and refresh tokens.</returns>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [DemoSessionEndpoint(DemoSessionEndpointKind.Refresh)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokensDto>> RefreshTokens(
        [FromBody] RefreshTokensCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Logs out the currently authenticated user by revoking their refresh token.
    /// </summary>
    /// <param name="command">The logout command containing the refresh token to be revoked.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating the successful logout.</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return NoContent();
    }
}
