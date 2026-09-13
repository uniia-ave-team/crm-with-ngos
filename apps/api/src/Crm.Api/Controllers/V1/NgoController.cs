using Asp.Versioning;
using Crm.Application.Dtos.Ngo;
using Crm.Application.Dtos.Ngo.Commands;
using Crm.Application.Dtos.Ngo.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for managing Non-Governmental Organization (NGO) data and settings.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class NgoController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retrieves the details of an NGO by its unique identifier.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A DTO containing the NGO details.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(NgoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetNgoQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new NGO in the system.
    /// </summary>
    /// <param name="command">The command containing the NGO creation data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the created NGO.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateNgoCommand command,
        CancellationToken cancellationToken)
    {
        var ngoId = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetAsync), null, ngoId);
    }

    /// <summary>
    /// Updates an existing NGO in the system.
    /// </summary>
    /// <param name="command">The command containing the updated NGO data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful update.</returns>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateAsync(
        [FromBody] UpdateNgoCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return NoContent();
    }
}
