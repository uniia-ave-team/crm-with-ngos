using Crm.Application.Dtos.Common;
using Mediator;

namespace Crm.Application.Features.Ngos.Queries;

/// <summary>
/// Represents a query to retrieve the actual file stream of the NGO's logo.
/// Requires no parameters as the system inherently operates with only one NGO instance.
/// </summary>
public record GetNgoLogoQuery : IQuery<FileDto>;
