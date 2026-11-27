using Crm.Api.Security;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Commands;
using Crm.Application.Features.Users.Queries;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <content>
/// Endpoints for generating, validating, listing, revoking, and consuming user password reset tokens.
/// </content>
public sealed partial class UsersController
{
    /// <summary>
    /// Validates a password reset token and returns the target account's identifying details
    /// (<c>email</c>, <c>firstName</c>, <c>lastName</c>, and <c>expiresAt</c>) so the user can confirm
    /// which account's password is being reset before submitting a new password.
    /// </summary>
    /// <param name="query">The query containing the password reset token.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The target account details associated with the valid password reset token.</returns>
    [HttpGet("reset-password/validate")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PasswordResetDetailsDto>> ValidatePasswordResetToken(
        [FromQuery] ValidatePasswordResetTokenQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Resets a user's password using a valid single-use restore token and sets a new password.
    /// </summary>
    /// <param name="command">The command containing the reset token and the new password.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Generates a short opaque password reset token for a specific user.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The generated password restore token and its expiration timestamp.</returns>
    [HttpPost("{id:guid}/password-reset-token")]
    [HasAccessRight(AccessRight.CanResetPassword)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TokenResultDto>> GeneratePasswordResetToken(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GenerateRestorePasswordTokenCommand(id), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Retrieves a paginated, filtered, and sorted list of active (unexpired) password reset tokens.
    /// </summary>
    /// <param name="query">Query parameters for search filtering, sorting, pagination, and optional <c>userId</c>/<c>createdBy</c> filters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated list of active password reset tokens.</returns>
    [HttpGet("password-reset-tokens")]
    [HasAccessRight(AccessRight.ManageUserActionTokens)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<ActivePasswordResetTokenDto>>> GetActivePasswordResetTokens(
        [FromQuery] GetActivePasswordResetTokensQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Permanently revokes an active password reset token by its identifier so the reset link can no longer be used.
    /// </summary>
    /// <param name="id">The unique identifier of the password reset token to revoke.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpDelete("password-reset-tokens/{id:guid}")]
    [HasAccessRight(AccessRight.ManageUserActionTokens)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokePasswordResetToken(
        Guid id,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new RevokeActionTokenCommand(id, ActionTokenType.PasswordReset), cancellationToken);
        return NoContent();
    }
}
