using MediatR;

namespace Crm.Application.Dtos.Role.Queries;

/// <summary>
/// Represents a query to retrieve all system-defined access rights available for assignment to roles.
/// Implements the MediatR query pattern returning a collection of access right string values.
/// </summary>
public record GetAvailableAccessRightsQuery : IRequest<IReadOnlySet<string>>;
