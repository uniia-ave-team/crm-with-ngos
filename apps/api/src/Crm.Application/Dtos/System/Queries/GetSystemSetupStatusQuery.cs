using MediatR;

namespace Crm.Application.Dtos.System.Queries;

/// <summary>
/// Represents a query to retrieve the current onboarding and setup status of the CRM system.
/// </summary>
public record GetSystemSetupStatusQuery : IRequest<SystemSetupStatusDto>;
