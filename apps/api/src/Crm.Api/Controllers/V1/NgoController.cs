using System.Net.Mime;
using Asp.Versioning;
using Crm.Api.Consts;
using Crm.Api.Extensions;
using Crm.Api.Security;
using Crm.Application.Dtos.Ngos;
using Crm.Application.Features.Ngos.Commands;
using Crm.Application.Features.Ngos.Queries;
using Crm.Domain.Enums;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for managing Non-Governmental Organization (NGO) data and settings.
/// </summary>
/// <param name="mediator">The mediator instance for handling requests and responses.</param>
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route(ApiRouteConstants.DefaultControllerRoute)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class NgoController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retrieves the details of the system's NGO.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A DTO containing the NGO details.</returns>
    [HttpGet(Name = nameof(Get))]
    [Authorize]
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
    [Authorize]
    [ResponseCache(Duration = ResponseCacheConstants.ThirtyDays, Location = ResponseCacheLocation.Client)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogo(CancellationToken cancellationToken)
    {
        var fileDto = await mediator.Send(new GetNgoLogoQuery(), cancellationToken);
        return this.ServeFile(fileDto);
    }

    /// <summary>
    /// Creates a new NGO in the system.
    /// </summary>
    /// <param name="command">The command containing the NGO creation data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A status code indicating successful creation.</returns>
    [HttpPost]
    [HasAccessRight(AccessRight.CreateNgo)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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
    /// <returns>An empty OK response indicating successful upload.</returns>
    [HttpPost("logo")]
    [HasAccessRight(AccessRight.UpdateNgo)]
    [RequestSizeLimit(FileLimitConstants.MaxImageUploadSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileLimitConstants.MaxImageUploadSize)]
    [Consumes(MediaTypeNames.Multipart.FormData)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> UploadLogo(
        IFormFile file,
        CancellationToken cancellationToken)
    {
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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateNgoCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return NoContent();
    }
}
