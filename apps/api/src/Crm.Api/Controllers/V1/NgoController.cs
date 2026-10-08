using System.Net.Mime;
using Asp.Versioning;
using Crm.Api.Consts;
using Crm.Api.Extensions;
using Crm.Api.Security;
using Crm.Application.Dtos.Ngo;
using Crm.Application.Dtos.Ngo.Commands;
using Crm.Application.Dtos.Ngo.Queries;
using Crm.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for managing Non-Governmental Organization (NGO) data and settings.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class NgoController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retrieves the details of an NGO by its unique identifier.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A DTO containing the NGO details.</returns>
    [HttpGet(Name = nameof(Get))]
    [HasAccessRight(AccessRight.ViewNgo)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NgoDto>> Get(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetNgoQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves the NGO's logo as a file stream.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A file stream containing the logo image.</returns>
    [HttpGet("logo")]
    [HasAccessRight(AccessRight.ViewNgo)]
    [ResponseCache(Duration = ResponseCacheConstants.ThirtyDays, Location = ResponseCacheLocation.Client)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogo(CancellationToken cancellationToken)
    {
        var fileDto = await mediator.Send(new GetNgoLogoQuery(), cancellationToken);

        return File(fileDto.Stream, fileDto.GetContentType(), enableRangeProcessing: true);
    }

    /// <summary>
    /// Creates a new NGO in the system.
    /// </summary>
    /// <param name="command">The command containing the NGO creation data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the created NGO.</returns>
    [HttpPost]
    [HasAccessRight(AccessRight.CreateNgo)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateNgoCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return CreatedAtRoute(nameof(Get), null, null);
    }

    /// <summary>
    /// Uploads a new logo for the NGO.
    /// </summary>
    /// <param name="file">The logo file to upload.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The URL of the uploaded logo.</returns>
    [HttpPost("logo")]
    [HasAccessRight(AccessRight.UpdateNgo)]
    [RequestSizeLimit(FileLimitConstants.MaxImageUploadSize)]
    [Consumes(MediaTypeNames.Multipart.FormData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadLogo(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A file is required.");
        }

        using var stream = file.OpenReadStream();

        await mediator.Send(new UploadNgoLogoCommand(stream, file.FileName), cancellationToken);

        return Ok();
    }

    /// <summary>
    /// Updates an existing NGO in the system.
    /// </summary>
    /// <param name="command">The command containing the updated NGO data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful update.</returns>
    [HttpPut]
    [HasAccessRight(AccessRight.UpdateNgo)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateNgoCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return NoContent();
    }
}
