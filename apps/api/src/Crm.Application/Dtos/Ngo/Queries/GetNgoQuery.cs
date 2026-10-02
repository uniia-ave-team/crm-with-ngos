using MediatR;

namespace Crm.Application.Dtos.Ngo.Queries;
/// <summary>
/// Represents a query to retrieve details of the system's NGO.
/// </summary>
public record GetNgoQuery() : IRequest<NgoDto?>;
