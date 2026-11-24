using Crm.Application.Dtos.Common;
using Mediator;

namespace Crm.Application.Features.System.Queries;

/// <summary>
/// Represents a query to retrieve identity system configuration options and password complexity requirements.
/// </summary>
public record GetIdentityOptionsQuery : IQuery<IdentityOptionsDto>;
