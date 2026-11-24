using Asp.Versioning;
using Crm.Api.Consts;
using Crm.Application.Dtos.Common;
using Crm.Application.Dtos.System;
using Crm.Application.Features.System.Queries;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers.V1;

/// <summary>
/// Controller responsible for handling system-related operations, such as retrieving the current onboarding and configuration options.
/// </summary>
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route(ApiRouteConstants.DefaultControllerRoute)]
public sealed class SystemController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Retrieves the current onboarding and setup status of the CRM system.
    /// Used by the frontend to determine whether to show the initial setup wizard.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The setup and onboarding status of the CRM.</returns>
    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemStatusDto>> GetSystemStatus(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSystemSetupStatusQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves system identity configuration settings and password complexity requirements.
    /// Used by the frontend to dynamically validate password inputs during registration and password reset.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The system identity settings and password requirements.</returns>
    [HttpGet("identity-options")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IdentityOptionsDto>> GetIdentityOptions(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetIdentityOptionsQuery(), cancellationToken);
        return Ok(result);
    }
}
