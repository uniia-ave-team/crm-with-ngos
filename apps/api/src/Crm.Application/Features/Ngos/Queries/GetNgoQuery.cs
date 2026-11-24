using Crm.Application.Dtos.Ngos;
using Mediator;

namespace Crm.Application.Features.Ngos.Queries;

/// <summary>
/// Represents a query to retrieve details of the system's NGO.
/// </summary>
public record GetNgoQuery : IQuery<NgoDto>;
