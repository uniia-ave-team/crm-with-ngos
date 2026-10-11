using Crm.Application.Dtos.Common;
using MediatR;

namespace Crm.Application.Dtos.User.Queries;

/// <summary>
/// Represents a query to retrieve the profile image (avatar) file stream for a specific user.
/// </summary>
/// <param name="UserId">The unique identifier of the user whose image is being requested.</param>
public record GetUserAvatarQuery(Guid UserId) : IRequest<FileDto>;
