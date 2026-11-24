using Crm.Application.Dtos.LoginPageImages;
using Mediator;

namespace Crm.Application.Features.LoginPageImages.Queries;

/// <summary>
/// Represents a query to retrieve detailed information about a specific login page image by its ID.
/// </summary>
/// <param name="Id">The unique identifier of the login page image to retrieve.</param>
public record GetLoginPageImageByIdQuery(Guid Id) : IQuery<LoginPageImageDto>;
