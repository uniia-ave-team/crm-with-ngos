using Crm.Application.Dtos.System;
using Mediator;

namespace Crm.Application.Features.System.Queries;

/// <summary>
/// Represents a query to retrieve the current onboarding and setup status of the CRM system.
/// </summary>
public record GetSystemSetupStatusQuery : IQuery<SystemStatusDto>;
