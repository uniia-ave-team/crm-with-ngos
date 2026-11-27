using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Users;
using Mediator;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Represents a query to retrieve ordered global user custom field column definitions
/// visible to the requesting user.
/// </summary>
public record GetUserCustomFieldsQuery : IQuery<IReadOnlyList<UserCustomFieldDto>>, ICurrentUserRolesAware
{
    /// <summary>
    /// Gets a value indicating whether private field definitions should always be included
    /// (set internally for self-profile form endpoints).
    /// </summary>
    [JsonIgnore]
    public bool IncludePrivate { get; init; }

    /// <summary>
    /// Gets or sets the role identifiers of the currently authenticated user, populated by the request pipeline.
    /// </summary>
    [JsonIgnore]
    public List<Guid> CurrentUserRoleIds { get; set; } = [];
}
