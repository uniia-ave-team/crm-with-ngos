using System.Net.Mime;
using Asp.Versioning;
using Crm.Api.Consts;
using Crm.Api.Extensions;
using Crm.Api.Security;
using Crm.Application.Dtos.LoginPageImage;
using Crm.Application.Dtos.LoginPageImage.Commands;
using Crm.Application.Dtos.LoginPageImage.Queries;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for managing login page images.
/// </summary>
/// <param name="mediator">The mediator instance for handling requests and responses.</param>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class LoginPageImagesController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retrieves a specific login page image by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the image to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The details of the requested login page image.</returns>
    [HttpGet("{id:guid}", Name = nameof(GetById))]
    [HasAccessRight(AccessRight.ViewLoginPageImages)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoginPageImageDto>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetLoginPageImageByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new login page image in the system.
    /// </summary>
    /// <param name="command">The command containing the URL or storage path of the new image.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the newly created image.</returns>
    [HttpPost]
    [HasAccessRight(AccessRight.CreateLoginPageImages)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Guid>> Create(
        [FromBody] CreateLoginPageImageCommand command,
        CancellationToken cancellationToken)
    {
        var loginPageImageId = await mediator.Send(command, cancellationToken);

        return CreatedAtRoute(
            nameof(GetById),
            new { id = loginPageImageId },
            new { Id = loginPageImageId });
    }

    /// <summary>
    /// Retrieves the file stream of a specific login page image.
    /// This endpoint is unprotected and intended for public use on the login screen.
    /// </summary>
    /// <param name="id">The unique identifier of the image file to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A file stream containing the login page image.</returns>
    [HttpGet("{id:guid}/image")]
    [AllowAnonymous]
    [ResponseCache(Duration = ResponseCacheConstants.ThirtyDays, Location = ResponseCacheLocation.Client)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var fileDto = await mediator.Send(new GetLoginPageImageFileQuery(id), cancellationToken);

        return File(fileDto.Stream, fileDto.GetContentType(), enableRangeProcessing: true);
    }

    /// <summary>
    /// Uploads a new login page image file and creates its record in the system.
    /// </summary>
    /// <param name="file">The login page image file to upload.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The unique identifier of the newly created image record.</returns>
    [HttpPost]
    [HasAccessRight(AccessRight.CreateLoginPageImages)]
    [Consumes(MediaTypeNames.Multipart.FormData)]
    [RequestSizeLimit(FileLimitConstants.MaxImageUploadSize)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Guid>> Upload(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A file is required.");
        }

        using var stream = file.OpenReadStream();

        var loginPageImageId = await mediator.Send(new UploadLoginPageImageCommand(stream, file.FileName), cancellationToken);

        return CreatedAtRoute(
            nameof(GetById),
            new { id = loginPageImageId },
            new { Id = loginPageImageId });
    }

    /// <summary>
    /// Deletes an existing login page image by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the image to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An empty result indicating successful deletion.</returns>
    [HttpDelete("{id:guid}")]
    [HasAccessRight(AccessRight.DeleteLoginPageImages)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteLoginPageImageCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Retrieves a paginated, filtered, and sorted list of available login page images.
    /// </summary>
    /// <param name="query">The pagination, filtering, and sorting parameters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated collection of login page image details.</returns>
    [HttpGet]
    [HasAccessRight(AccessRight.ViewLoginPageImages)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<LoginPageImageDto>>> GetPaged(
        [FromQuery] GetPagedLoginPageImagesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a random login page image.
    /// This endpoint is unprotected and intended for public use on the login screen.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The details of a random login page image.</returns>
    [HttpGet("random")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoginPageImageDto>> GetRandom(
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetRandomLoginPageImageQuery(), cancellationToken);

        return Ok(result);
    }
}
