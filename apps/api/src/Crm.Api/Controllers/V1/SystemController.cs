using Asp.Versioning;
using Crm.Application.Dtos.System;
using Crm.Application.Dtos.System.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for handling system-related operations, such as retrieving the current onboarding.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class SystemController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retrieves the current onboarding and setup status of the CRM system.
    /// Used by the frontend to determine whether to show the initial setup wizard.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(SystemSetupStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemStatusAsync(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSystemSetupStatusQuery(), cancellationToken);
        return Ok(result);
    }
}
