using Asp.Versioning;
using Crm.Api.Security;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Dtos.User.Queries;
using Crm.Application.Features.Users.Mappings;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Enums;
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
    [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsersAsync(
        [FromQuery] GetUsersQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves the detailed profile of a specific user.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The detailed profile of the user.</returns>
    [HttpGet("{id:guid}/profile")]
    [HasAccessRight(AccessRight.ViewUser)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserProfileAsync(
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
    [HttpGet("me/profile")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUserProfileAsync(CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetUserId();

        var result = await mediator.Send(new GetUserProfileQuery(currentUserId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new user (typically used only for the first system admin initialization).
    /// </summary>
    /// <param name="command">The details of the user to create.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the newly created user.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUserAsync(
        [FromBody] CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var userId = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetUserProfileAsync), new { id = userId }, new { UserId = userId });
    }

    /// <summary>
    /// Updates the profile details of an existing user.
    /// </summary>
    /// <param name="id">The unique identifier of the user to update.</param>
    /// <param name="command">The updated profile details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPut("{id:guid}/profile")]
    [HasAccessRight(AccessRight.UpdateUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUserProfileAsync(
        Guid id,
        [FromBody] UpdateProfileRequest command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command.ToUpdateUserProfileCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Updates the profile details of the currently authenticated user.
    /// </summary>
    /// <param name="command">The updated profile details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPut("me/profile")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateCurrentUserProfileAsync(
        [FromBody] UpdateProfileRequest command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command.ToUpdateUserProfileCommand(currentUserService.GetUserId()), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Securely changes the password for the currently authenticated user.
    /// </summary>
    /// <param name="command">The current and new password details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPut("me/password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMyPasswordAsync(
        [FromBody] UpdatePasswordRequest command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command.ToUpdatePasswordCommand(currentUserService.GetUserId()), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Activates a previously deactivated user.
    /// </summary>
    /// <param name="id">The unique identifier of the user to activate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPut("{id:guid}/activate")]
    [HasAccessRight(AccessRight.UpdateUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ActivateUserAsync(
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
    [HttpPut("{id:guid}/deactivate")]
    [HasAccessRight(AccessRight.DisableUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeactivateUserAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeactivateUserCommand(id, currentUserService.GetUserId()), cancellationToken);
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
    public async Task<IActionResult> AssignRoleAsync(
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
    public async Task<IActionResult> RemoveRoleAsync(
        Guid id,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new RemoveUserRoleCommand(id, roleId), cancellationToken);
        return NoContent();
    }
}
