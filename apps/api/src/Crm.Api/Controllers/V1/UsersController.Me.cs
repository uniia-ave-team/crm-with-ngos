using System.Net.Mime;
using Crm.Api.Consts;
using Crm.Api.Extensions;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Commands;
using Crm.Application.Features.Users.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <content>
/// Self-service (<c>/me</c>) endpoints for the currently authenticated user's profile, avatar, password, permissions, and custom fields.
/// </content>
public sealed partial class UsersController
{
    /// <summary>
    /// Retrieves the detailed profile of the currently authenticated user.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The detailed profile of the current user.</returns>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> GetCurrentUserProfile(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserProfileQuery(IsSelf: true), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Permanently deletes the currently authenticated user's account and all associated personal data (GDPR hard-delete).
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpDelete("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMe(CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteUserCommand(TargetCurrentUser: true), cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Updates the profile details of the currently authenticated user.
    /// </summary>
    /// <param name="command">The updated profile details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCurrentUserProfile(
        [FromBody] UpdateUserProfileCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command with { TargetCurrentUser = true }, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Retrieves the avatar of the currently authenticated user as a file stream.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A file stream containing the user's avatar image.</returns>
    [HttpGet("me/avatar")]
    [Authorize]
    [ResponseCache(Duration = ResponseCacheConstants.ThirtyDays, Location = ResponseCacheLocation.Client)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyAvatar(CancellationToken cancellationToken)
    {
        var fileDto = await mediator.Send(new GetUserAvatarQuery(TargetCurrentUser: true), cancellationToken);
        return this.ServeFile(fileDto);
    }

    /// <summary>
    /// Uploads or updates the avatar for the currently authenticated user.
    /// </summary>
    /// <param name="file">The avatar image file to upload.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An empty OK response indicating successful upload.</returns>
    [HttpPost("me/avatar")]
    [Authorize]
    [Consumes(MediaTypeNames.Multipart.FormData)]
    [RequestSizeLimit(FileLimitConstants.MaxImageUploadSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileLimitConstants.MaxImageUploadSize)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> UploadMyAvatar(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        using var stream = file.OpenReadStream();

        await mediator.Send(
            new UploadUserAvatarCommand(Guid.Empty, stream, file.FileName, TargetCurrentUser: true),
            cancellationToken);

        return Ok();
    }

    /// <summary>
    /// Securely changes the password for the currently authenticated user.
    /// The change signs the user out of all other sessions (their refresh tokens are revoked);
    /// the calling session must switch to the returned token pair to stay signed in.
    /// </summary>
    /// <param name="command">The current and new password details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A DTO containing a fresh JWT access token and refresh token for the calling session.</returns>
    [HttpPost("me/password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthTokensDto>> UpdateMyPassword(
        [FromBody] UpdatePasswordCommand command,
        CancellationToken cancellationToken)
    {
        var tokens = await mediator.Send(command, cancellationToken);

        return Ok(tokens);
    }

    /// <summary>
    /// Retrieves the list of unique UI permissions for the currently authenticated user.
    /// Used by the client application to build the user interface dynamically.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of access right strings.</returns>
    [HttpGet("me/permissions")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<string>>> GetMyPermissions(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserPermissionsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all global user custom field definitions (including private fields) for editing the current user's own profile.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An ordered list of all user custom field column definitions.</returns>
    [HttpGet("me/custom-fields")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<UserCustomFieldDto>>> GetMyCustomFieldDefinitions(
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserCustomFieldsQuery { IncludePrivate = true }, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Validates and partially updates the custom fields JSON payload for the currently authenticated user against the global schema.
    /// Only the keys supplied in <c>data</c> are merged onto the latest stored values; omitted keys remain untouched.
    /// </summary>
    /// <param name="command">The partial JSON object containing changed custom field values for the current user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPatch("me/custom-fields")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyCustomFields(
        [FromBody] UpdateUserCustomFieldsCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command with { TargetCurrentUser = true }, cancellationToken);
        return NoContent();
    }
}
