using Crm.Application.Dtos.LoginPageImages;
using Mediator;

namespace Crm.Application.Features.LoginPageImages.Queries;

/// <summary>
/// Represents a query to retrieve a random login page image.
/// This is typically used for unprotected endpoints, such as displaying a background on the login page.
/// </summary>
public record GetRandomLoginPageImageQuery : IQuery<LoginPageImageDto>;
