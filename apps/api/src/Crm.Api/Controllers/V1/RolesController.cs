using Asp.Versioning;
using Crm.Api.Security;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.Role.Commands;
using Crm.Application.Dtos.Role.Queries;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for managing system roles and their associated permissions.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class RolesController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retrieves a paginated, filtered, and sorted list of user roles.
    /// </summary>
    /// <param name="query">The query parameters for filtering and sorting.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated list of roles.</returns>
    [HttpGet]
    [HasAccessRight(AccessRight.ViewRole)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<RoleDto>>> GetRolesAsync(
        [FromQuery] GetAllRolesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves detailed information about a specific role by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the role.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The detailed role information.</returns>
    [HttpGet("{id:guid}")]
    [HasAccessRight(AccessRight.ViewRole)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDetailsDto>> GetRoleByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetRoleByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all system-defined access rights available for assignment to roles.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A collection of all available access right values.</returns>
    [HttpGet("permissions")]
    [HasAccessRight(AccessRight.ViewRole)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlySet<string>>> GetAvailableAccessRightsAsync(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAvailableAccessRightsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new role in the system.
    /// </summary>
    /// <param name="command">The details of the role to create.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the newly created role.</returns>
    [HttpPost]
    [HasAccessRight(AccessRight.CreateRole)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Guid>> CreateRoleAsync(
        [FromBody] CreateRoleCommand command,
        CancellationToken cancellationToken)
    {
        var roleId = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetRoleByIdAsync), new { id = roleId }, new { Id = roleId });
    }

    /// <summary>
    /// Updates an existing role's details.
    /// </summary>
    /// <param name="id">The unique identifier of the role to update.</param>
    /// <param name="request">The updated role details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPut("{id:guid}")]
    [HasAccessRight(AccessRight.UpdateRole)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateRoleAsync(
        Guid id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateRoleCommand(id, request.NewName);

        await mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes an existing role.
    /// </summary>
    /// <param name="id">The unique identifier of the role to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpDelete("{id:guid}")]
    [HasAccessRight(AccessRight.DeleteRole)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteRoleAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteRoleCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Adds a specific permission claim to an existing role.
    /// </summary>
    /// <param name="id">The unique identifier of the role.</param>
    /// <param name="claimValue">The value of the permission claim to add.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPost("{id:guid}/claims/{claimValue}")]
    [HasAccessRight(AccessRight.ManageRolePermissions)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddClaimToRoleAsync(
        Guid id,
        string claimValue,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new AddClaimToRoleCommand(id, claimValue), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Removes a specific permission claim from an existing role.
    /// </summary>
    /// <param name="id">The unique identifier of the role.</param>
    /// <param name="claimValue">The value of the permission claim to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpDelete("{id:guid}/claims/{claimValue}")]
    [HasAccessRight(AccessRight.ManageRolePermissions)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveClaimFromRoleAsync(
        Guid id,
        string claimValue,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new RemoveClaimFromRoleCommand(id, claimValue), cancellationToken);
        return NoContent();
    }
}
