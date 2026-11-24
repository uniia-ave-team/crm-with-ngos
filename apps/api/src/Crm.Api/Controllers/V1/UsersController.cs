using System.Net.Mime;
using Asp.Versioning;
using Crm.Api.Consts;
using Crm.Api.Extensions;
using Crm.Api.Security;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Commands;
using Crm.Application.Features.Users.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for managing user profiles, roles, and system access status.
/// </summary>
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route(ApiRouteConstants.DefaultControllerRoute)]
public sealed class UsersController(
    IMediator mediator,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>
    /// Retrieves a paginated and filtered list of users.
    /// </summary>
    /// <param name="query">The query parameters for filtering and pagination.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated list of users.</returns>
    [HttpGet]
    [HasAccessRight(AccessRight.ViewUser)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers(
        [FromQuery] GetUsersQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            query with
            {
                CurrentUserId = currentUserService.GetUserId(),
                CurrentUserRoleIds = currentUserService.GetRoleIds(),
            },
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Permanently deletes a specific user and all associated personal data (GDPR hard-delete).
    /// </summary>
    /// <param name="id">The unique identifier of the user to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpDelete("{id:guid}")]
    [HasAccessRight(AccessRight.DeleteUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteUserCommand(id, currentUserService.GetUserId()), cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Retrieves the detailed profile of a specific user.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The detailed profile of the user.</returns>
    [HttpGet("{id:guid}", Name = nameof(GetUserProfile))]
    [HasAccessRight(AccessRight.ViewUser)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> GetUserProfile(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetUserProfileQuery(id, currentUserService.GetUserId(), currentUserService.GetRoleIds()),
            cancellationToken);
        return Ok(result);
    }

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
        var currentUserId = currentUserService.GetUserId();

        var result = await mediator.Send(
            new GetUserProfileQuery(currentUserId, currentUserId, [], IsSelf: true),
            cancellationToken);
        return Ok(result);
    }

    // TODO: Add a middleware or filter that disables this endpoint entirely after the first user exists (or maybe add that to SystemController and disable it entirely).

    /// <summary>
    /// Creates a new user (typically used only for the first system admin initialization).
    /// </summary>
    /// <param name="command">The details of the user to create.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the newly created user.</returns>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Guid>> CreateUser(
        [FromBody] CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var userId = await mediator.Send(command, cancellationToken);

        return CreatedAtRoute(nameof(GetUserProfile), new { id = userId }, userId);
    }

    /// <summary>
    /// Updates the profile details of an existing user.
    /// </summary>
    /// <param name="id">The unique identifier of the user to update.</param>
    /// <param name="command">The updated profile details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPut("{id:guid}")]
    [HasAccessRight(AccessRight.UpdateUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserProfile(
        Guid id,
        [FromBody] UpdateUserProfileCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command with { UserId = id }, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Resets a user's password using a valid restore token and sets a new password.
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
    public async Task<IActionResult> DeleteMe(
        CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetUserId();
        await mediator.Send(new DeleteUserCommand(currentUserId, currentUserId), cancellationToken);

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
        await mediator.Send(command with { UserId = currentUserService.GetUserId() }, cancellationToken);

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
        var fileDto = await mediator.Send(new GetUserAvatarQuery(currentUserService.GetUserId()), cancellationToken);
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

        await mediator.Send(new UploadUserAvatarCommand(currentUserService.GetUserId(), stream, file.FileName), cancellationToken);

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
        var tokens = await mediator.Send(command with { UserId = currentUserService.GetUserId() }, cancellationToken);

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
        var result = await mediator.Send(new GetUserPermissionsQuery(currentUserService.GetRoleIds()), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Adds a custom key-value field to the currently authenticated user's profile.
    /// </summary>
    /// <param name="command">The custom field key and value details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPost("me/custom-fields")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMyCustomField(
        [FromBody] AddUserCustomFieldCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command with { UserId = currentUserService.GetUserId() }, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Removes a specific custom field from the currently authenticated user's profile.
    /// </summary>
    /// <param name="customFieldId">The unique identifier of the custom field to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpDelete("me/custom-fields/{customFieldId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMyCustomField(
        Guid customFieldId,
        CancellationToken cancellationToken)
    {
        var command = new RemoveUserCustomFieldCommand(customFieldId, currentUserService.GetUserId());

        await mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Updates the value of a specific custom field for the currently authenticated user.
    /// </summary>
    /// <param name="customFieldId">The unique identifier of the custom field to update.</param>
    /// <param name="command">The updated custom field value.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPut("me/custom-fields/{customFieldId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyCustomField(
        Guid customFieldId,
        [FromBody] UpdateUserCustomFieldCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            command with { CustomFieldId = customFieldId, UserId = currentUserService.GetUserId() },
            cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Retrieves the avatar of a specific user as a file stream.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A file stream containing the user's avatar image.</returns>
    [HttpGet("{id:guid}/avatar")]
    [HasAccessRight(AccessRight.ViewUser)]
    [ResponseCache(Duration = ResponseCacheConstants.ThirtyDays, Location = ResponseCacheLocation.Client)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserAvatar(
        Guid id,
        CancellationToken cancellationToken)
    {
        var fileDto = await mediator.Send(new GetUserAvatarQuery(id), cancellationToken);
        return this.ServeFile(fileDto);
    }

    /// <summary>
    /// Uploads or updates the avatar for a specific user.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <param name="file">The avatar image file to upload.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An empty OK response indicating successful upload.</returns>
    [HttpPost("{id:guid}/avatar")]
    [HasAccessRight(AccessRight.UpdateUser)]
    [Consumes(MediaTypeNames.Multipart.FormData)]
    [RequestSizeLimit(FileLimitConstants.MaxImageUploadSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileLimitConstants.MaxImageUploadSize)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> UploadUserAvatar(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        using var stream = file.OpenReadStream();

        await mediator.Send(new UploadUserAvatarCommand(id, stream, file.FileName), cancellationToken);

        return Ok();
    }

    /// <summary>
    /// Activates a previously deactivated user.
    /// </summary>
    /// <param name="id">The unique identifier of the user to activate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPost("{id:guid}/activate")]
    [HasAccessRight(AccessRight.UpdateUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new ActivateUserCommand(id, currentUserService.GetUserId()), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deactivates an active user (soft delete and lock identity account).
    /// </summary>
    /// <param name="id">The unique identifier of the user to deactivate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPost("{id:guid}/deactivate")]
    [HasAccessRight(AccessRight.DisableUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeactivateUserCommand(id, currentUserService.GetUserId()), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Generates a secure password reset token for a specific user.
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

    // TODO: Implement GET api/v1/users/custom-fields/keys endpoint to return distinct custom field keys across all users for dynamic frontend table columns.
    // TODO: Implement bulk custom fields management (e.g. adding a custom field schema / field definition to all users at once).

    /// <summary>
    /// Adds a custom key-value field to a specific user's profile.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <param name="command">The custom field key and value details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPost("{id:guid}/custom-fields")]
    [HasAccessRight(AccessRight.UpdateUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddCustomField(
        Guid id,
        [FromBody] AddUserCustomFieldCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command with { UserId = id }, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Removes a specific custom field from a specific user's profile.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <param name="customFieldId">The unique identifier of the custom field to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpDelete("{id:guid}/custom-fields/{customFieldId:guid}")]
    [HasAccessRight(AccessRight.UpdateUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveCustomField(
        Guid id,
        Guid customFieldId,
        CancellationToken cancellationToken)
    {
        var command = new RemoveUserCustomFieldCommand(customFieldId, id);

        await mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Updates the value of a specific custom field for a specific user.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <param name="customFieldId">The unique identifier of the custom field to update.</param>
    /// <param name="command">The updated custom field value.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPut("{id:guid}/custom-fields/{customFieldId:guid}")]
    [HasAccessRight(AccessRight.UpdateUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCustomField(
        Guid id,
        Guid customFieldId,
        [FromBody] UpdateUserCustomFieldCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command with { CustomFieldId = customFieldId, UserId = id }, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Assigns a new role to the specified user.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <param name="roleId">The unique identifier of the role to assign.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPost("{id:guid}/roles/{roleId:guid}")]
    [HasAccessRight(AccessRight.AssignRoleToUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole(
        Guid id,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var command = new AssignUserRoleCommand(id, roleId);

        await mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Removes a specific role from the specified user.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <param name="roleId">The unique identifier of the role to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [HasAccessRight(AccessRight.AssignRoleToUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveRole(
        Guid id,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new RemoveUserRoleCommand(id, roleId), cancellationToken);
        return NoContent();
    }
}
