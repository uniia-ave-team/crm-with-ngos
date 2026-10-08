using Crm.Application.Dtos.Common;
using MediatR;

namespace Crm.Application.Dtos.LoginPageImage.Queries;

/// <summary>
/// Represents a query to retrieve the actual file stream of a login page image by its unique identifier.
/// </summary>
/// <param name="Id">The unique identifier of the login page image.</param>
public record GetLoginPageImageFileQuery(Guid Id) : IRequest<FileDto>;
