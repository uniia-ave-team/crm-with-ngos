using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Users;
using Mediator;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Represents a query to retrieve the detailed profile of a specific user.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="IsSelf">A value indicating whether the request is made by the user for their own profile.</param>
public record GetUserProfileQuery(
    Guid UserId = default,
    bool IsSelf = false) : IQuery<UserProfileDto>, ICurrentUserAware, ICurrentUserRolesAware, ISelfTargetableUserRequest
{
    /// <inheritdoc />
    public Guid UserId { get; set; } = UserId;

    /// <summary>
    /// Gets or sets the unique identifier of the currently authenticated user, populated by the request pipeline.
    /// </summary>
    [JsonIgnore]
    public Guid CurrentUserId { get; set; }

    /// <summary>
    /// Gets or sets the role identifiers of the currently authenticated user, populated by the request pipeline.
    /// </summary>
    [JsonIgnore]
    public List<Guid> CurrentUserRoleIds { get; set; } = [];

    /// <inheritdoc />
    bool ISelfTargetableUserRequest.TargetCurrentUser => IsSelf;
}
