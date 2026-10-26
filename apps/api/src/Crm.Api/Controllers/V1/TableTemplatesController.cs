using Asp.Versioning;
using Crm.Api.Dtos;
using Crm.Api.Security;
using Crm.Application.Dtos.CustomTemplates;
using Crm.Application.Dtos.CustomTemplates.Commands;
using Crm.Application.Dtos.CustomTemplates.Queries;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for managing reusable custom table templates.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class TableTemplatesController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retrieves a paginated list of available table templates.
    /// </summary>
    /// <param name="query">The query parameters for filtering, sorting, and pagination.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated collection of table template DTOs.</returns>
    [HttpGet]
    [HasAccessRight(AccessRight.ViewTableTemplate)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TableTemplateDto>>> GetPaged(
        [FromQuery] GetTableTemplatesPagedQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a specific table template by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the table template.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The detailed DTO containing template properties and field blueprints.</returns>
    [HttpGet("{id:guid}", Name = nameof(GetTemplateById))]
    [HasAccessRight(AccessRight.ViewTableTemplate)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TableTemplateDto>> GetTemplateById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTableTemplateByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new reusable table template blueprint.
    /// </summary>
    /// <param name="command">The command containing template creation details and field blueprints.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the created table template.</returns>
    [HttpPost]
    [HasAccessRight(AccessRight.CreateTableTemplate)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTableTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return CreatedAtRoute(nameof(GetTemplateById), new { id }, id);
    }

    /// <summary>
    /// Updates an existing table template blueprint and synchronizes its fields.
    /// </summary>
    /// <param name="id">The unique identifier of the table template to update.</param>
    /// <param name="request">The request containing the updated template data and field collection.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful update.</returns>
    [HttpPut("{id:guid}")]
    [HasAccessRight(AccessRight.UpdateTableTemplate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTableTemplateRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(request.Adapt<UpdateTableTemplateCommand>() with { Id = id }, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes a custom table template from the system. System presets are protected from deletion.
    /// </summary>
    /// <param name="id">The unique identifier of the table template to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful deletion.</returns>
    [HttpDelete("{id:guid}")]
    [HasAccessRight(AccessRight.DeleteTableTemplate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTableTemplateCommand(id), cancellationToken);
        return NoContent();
    }
}
