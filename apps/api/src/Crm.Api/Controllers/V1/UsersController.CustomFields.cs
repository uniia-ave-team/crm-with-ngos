using Crm.Api.Security;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Commands;
using Crm.Application.Features.Users.Queries;
using Crm.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <content>
/// Endpoints for managing the global user custom field schema and individual user custom field JSON payloads.
/// </content>
public sealed partial class UsersController
{
    /// <summary>
    /// Retrieves the ordered list of global user custom field definitions visible to the current user
    /// (for rendering dynamic user table column headers and smart sort controls).
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An ordered list of visible user custom field column definitions.</returns>
    [HttpGet("custom-fields")]
    [HasAccessRight(AccessRight.ViewUser)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<UserCustomFieldDto>>> GetUserCustomFields(
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetUserCustomFieldsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Atomically synchronizes the global user custom field schema (creating, updating, reordering, renaming, or removing columns)
    /// and updates all users' JSONB custom field payloads in a single transaction.
    /// </summary>
    /// <param name="command">The complete target ordered list of user custom field definitions.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPut("custom-fields")]
    [HasAccessRight(AccessRight.UpdateUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SyncUserCustomFields(
        [FromBody] SyncUserCustomFieldsCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Validates and updates the custom fields JSON payload for a specific user against the global schema.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <param name="command">The JSON object containing the user's custom field values.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status indicating the outcome of the operation.</returns>
    [HttpPut("{id:guid}/custom-fields")]
    [HasAccessRight(AccessRight.UpdateUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserCustomFields(
        Guid id,
        [FromBody] UpdateUserCustomFieldsCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command with { UserId = id }, cancellationToken);
        return NoContent();
    }
}
