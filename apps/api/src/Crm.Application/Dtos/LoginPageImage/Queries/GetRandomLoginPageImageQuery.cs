using MediatR;

namespace Crm.Application.Dtos.LoginPageImage.Queries;

/// <summary>
/// Represents a query to retrieve a random login page image.
/// This is typically used for unprotected endpoints, such as displaying a background on the login page.
/// </summary>
public record GetRandomLoginPageImageQuery : IRequest<LoginPageImageDto>;
