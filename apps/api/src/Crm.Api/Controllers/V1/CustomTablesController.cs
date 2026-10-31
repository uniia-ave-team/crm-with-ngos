using Asp.Versioning;
using Crm.Api.Dtos;
using Crm.Api.Security;
using Crm.Application.Dtos.CustomTables;
using Crm.Application.Dtos.CustomTables.Commands;
using Crm.Application.Dtos.CustomTables.Queries;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for managing NGO custom tables and schema definitions.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class CustomTablesController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retrieves a paginated list of custom tables belonging to an NGO.
    /// </summary>
    /// <param name="query">The query parameters for filtering, sorting, and pagination.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated collection of custom table summary DTOs.</returns>
    [HttpGet]
    [HasAccessRight(AccessRight.ViewCustomTable)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CustomTableSummaryDto>>> GetPaged(
        [FromQuery] GetCustomTablesPagedQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves full custom table schema including all defined fields by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the custom table.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The detailed custom table schema DTO.</returns>
    [HttpGet("{id:guid}", Name = nameof(GetCustomTableById))]
    [HasAccessRight(AccessRight.ViewCustomTable)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomTableDto>> GetCustomTableById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCustomTableByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new custom table from scratch without using a template.
    /// </summary>
    /// <param name="command">The command containing custom table creation details and initial fields.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the created custom table.</returns>
    [HttpPost]
    [HasAccessRight(AccessRight.CreateCustomTable)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomTableCommand command,
        CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return CreatedAtRoute(nameof(GetCustomTableById), new { id }, id);
    }

    /// <summary>
    /// Instantiates a new custom table from an existing template preset.
    /// </summary>
    /// <param name="command">The command containing source template ID and target table details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the newly created custom table.</returns>
    [HttpPost("from-template")]
    [HasAccessRight(AccessRight.CreateCustomTable)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateFromTemplate(
        [FromBody] CreateCustomTableFromTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return CreatedAtRoute(nameof(GetCustomTableById), new { id }, id);
    }

    /// <summary>
    /// Updates metadata (Name and Description) of an existing custom table.
    /// </summary>
    /// <param name="id">The unique identifier of the custom table to update.</param>
    /// <param name="request">The request containing updated metadata.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful update.</returns>
    [HttpPut("{id:guid}")]
    [HasAccessRight(AccessRight.UpdateCustomTable)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCustomTableRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(request.ToCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes a custom table along with all its schema fields and stored records.
    /// </summary>
    /// <param name="id">The unique identifier of the custom table to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful deletion.</returns>
    [HttpDelete("{id:guid}")]
    [HasAccessRight(AccessRight.DeleteCustomTable)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteCustomTableCommand(id), cancellationToken);
        return NoContent();
    }
}
