using System.Net.Mime;
using Asp.Versioning;
using Crm.Api.Consts;
using Crm.Api.Dtos;
using Crm.Api.Extensions;
using Crm.Api.Security;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Dtos.User.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for managing user profiles, roles, and system access status.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class UsersController(
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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers(
        [FromQuery] GetUsersQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteUserCommand(id), cancellationToken);

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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> GetUserProfile(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserProfileQuery(id), cancellationToken);
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

        var result = await mediator.Send(new GetUserProfileQuery(currentUserId, IsSelf: true), cancellationToken);
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
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> CreateUser(
        [FromBody] CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var userId = await mediator.Send(command, cancellationToken);

        return CreatedAtRoute(
            nameof(GetUserProfile),
            new { id = userId },
            new { UserId = userId });
    }

    /// <summary>
    /// Updates the profile details of an existing user.
    /// </summary>
    /// <param name="id">The unique identifier of the user to update.</param>
    /// <param name="request">The updated profile details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPut("{id:guid}")]
    [HasAccessRight(AccessRight.UpdateUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserProfile(
        Guid id,
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var command = request.Adapt<UpdateUserProfileCommand>() with { UserId = id };

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
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteMe(
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteUserCommand(currentUserService.GetUserId()), cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Updates the profile details of the currently authenticated user.
    /// </summary>
    /// <param name="request">The updated profile details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateCurrentUserProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var command = request.Adapt<UpdateUserProfileCommand>() with { UserId = currentUserService.GetUserId() };

        await mediator.Send(command, cancellationToken);

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

        return File(fileDto.Stream, fileDto.GetContentType(), enableRangeProcessing: true);
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadMyAvatar(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A file is required.");
        }

        using var stream = file.OpenReadStream();

        await mediator.Send(new UploadUserAvatarCommand(currentUserService.GetUserId(), stream, file.FileName), cancellationToken);

        return Ok();
    }

    /// <summary>
    /// Securely changes the password for the currently authenticated user.
    /// </summary>
    /// <param name="request">The current and new password details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPost("me/password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMyPassword(
        [FromBody] UpdatePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var command = request.Adapt<UpdatePasswordCommand>() with { UserId = currentUserService.GetUserId() };

        await mediator.Send(command, cancellationToken);

        return NoContent();
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
    /// <param name="request">The custom field key and value details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPost("me/custom-fields")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddMyCustomField(
        [FromBody] AddCustomFieldRequest request,
        CancellationToken cancellationToken)
    {
        var command = request.Adapt<AddUserCustomFieldCommand>() with { UserId = currentUserService.GetUserId() };

        await mediator.Send(command, cancellationToken);
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
    /// <param name="request">The updated custom field value.</param>
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
        [FromBody] UpdateCustomFieldRequest request,
        CancellationToken cancellationToken)
    {
        var command = request.Adapt<UpdateUserCustomFieldCommand>() with
        {
            CustomFieldId = customFieldId,
            UserId = currentUserService.GetUserId(),
        };

        await mediator.Send(command, cancellationToken);
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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserAvatar(
        Guid id,
        CancellationToken cancellationToken)
    {
        var fileDto = await mediator.Send(new GetUserAvatarQuery(id), cancellationToken);

        return File(fileDto.Stream, fileDto.GetContentType(), enableRangeProcessing: true);
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadUserAvatar(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A file is required.");
        }

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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeactivateUserCommand(id, currentUserService.GetUserId()), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Adds a custom key-value field to a specific user's profile.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <param name="request">The custom field key and value details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPost("{id:guid}/custom-fields")]
    [HasAccessRight(AccessRight.UpdateUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddCustomField(
        Guid id,
        [FromBody] AddCustomFieldRequest request,
        CancellationToken cancellationToken)
    {
        var command = request.Adapt<AddUserCustomFieldCommand>() with { UserId = id };

        await mediator.Send(command, cancellationToken);
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
    /// <param name="request">The updated custom field value.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPut("{id:guid}/custom-fields/{customFieldId:guid}")]
    [HasAccessRight(AccessRight.UpdateUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCustomField(
        Guid id,
        Guid customFieldId,
        [FromBody] UpdateCustomFieldRequest request,
        CancellationToken cancellationToken)
    {
        var command = request.Adapt<UpdateUserCustomFieldCommand>() with
        {
            CustomFieldId = customFieldId,
            UserId = id,
        };

        await mediator.Send(command, cancellationToken);
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
