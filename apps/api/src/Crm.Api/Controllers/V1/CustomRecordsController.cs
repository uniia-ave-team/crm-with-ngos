using Asp.Versioning;
using Crm.Api.Consts;
using Crm.Api.Security;
using Crm.Application.Dtos.CustomRecords;
using Crm.Application.Features.CustomRecords.Commands;
using Crm.Application.Features.CustomRecords.Queries;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for managing dynamic custom records and data payloads.
/// </summary>
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route(ApiRouteConstants.DefaultControllerRoute)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class CustomRecordsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retrieves a paginated list of custom records for a specific custom table.
    /// </summary>
    /// <param name="query">The query parameters for filtering, sorting, and pagination.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated collection of custom record DTOs.</returns>
    [HttpGet]
    [HasAccessRight(AccessRight.ViewCustomRecord)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<CustomRecordDto>>> GetPaged(
        [FromQuery] GetCustomRecordsPagedQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single custom record by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the custom record.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The detailed custom record DTO.</returns>
    [HttpGet("{id:guid}", Name = nameof(GetCustomRecordById))]
    [HasAccessRight(AccessRight.ViewCustomRecord)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomRecordDto>> GetCustomRecordById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCustomRecordByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new dynamic data record inside a target custom table.
    /// </summary>
    /// <param name="command">The command containing the target custom table ID and initial JSON payload data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the newly created custom record.</returns>
    [HttpPost]
    [HasAccessRight(AccessRight.CreateCustomRecord)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Guid>> Create(
        [FromBody] CreateCustomRecordCommand command,
        CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return CreatedAtRoute(nameof(GetCustomRecordById), new { id }, id);
    }

    /// <summary>
    /// Updates the JSON data payload of an existing custom record.
    /// </summary>
    /// <param name="id">The unique identifier of the custom record to update.</param>
    /// <param name="command">The command containing the updated JSON data payload.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful update.</returns>
    [HttpPut("{id:guid}")]
    [HasAccessRight(AccessRight.UpdateCustomRecord)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCustomRecordCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes a specific custom record by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the custom record to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful deletion.</returns>
    [HttpDelete("{id:guid}")]
    [HasAccessRight(AccessRight.DeleteCustomRecord)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteCustomRecordCommand(id), cancellationToken);
        return NoContent();
    }
}
