using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Common;
using Mediator;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Represents a query to retrieve the profile image (avatar) file stream for a specific user.
/// </summary>
/// <param name="UserId">The unique identifier of the user whose image is being requested.</param>
/// <param name="TargetCurrentUser">Indicates whether to resolve <paramref name="UserId"/> from the currently authenticated user (<c>/me</c> route).</param>
public record GetUserAvatarQuery(
    Guid UserId = default,
    bool TargetCurrentUser = false) : IQuery<FileDto>, ISelfTargetableUserRequest
{
    /// <inheritdoc />
    public Guid UserId { get; set; } = UserId;
}
