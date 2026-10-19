using Crm.Application.Dtos.Common;
using MediatR;

namespace Crm.Application.Dtos.System.Queries;

/// <summary>
/// Represents a query to retrieve identity system configuration options and password complexity requirements.
/// </summary>
public record GetIdentityOptionsQuery : IRequest<IdentityOptionsDto>;
