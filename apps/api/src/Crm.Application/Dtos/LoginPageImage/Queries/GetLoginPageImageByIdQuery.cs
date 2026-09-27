using MediatR;

namespace Crm.Application.Dtos.LoginPageImage.Queries;

/// <summary>
/// Represents a query to retrieve detailed information about a specific login page image by its ID.
/// </summary>
/// <param name="Id">The unique identifier of the login page image to retrieve.</param>
public record GetLoginPageImageByIdQuery(Guid Id) : IRequest<LoginPageImageDto>;
