namespace Crm.Application.Common.Interfaces;

/// <summary>
/// Marker interface for requests that require the currently authenticated user's role identifiers,
/// automatically populated by <see cref="Behaviors.CurrentUserContextBehavior{TRequest, TResponse}"/>.
/// </summary>
public interface ICurrentUserRolesAware : ICurrentUserContextRequest
{
    /// <summary>
    /// Gets or sets the role identifiers of the currently authenticated user.
    /// </summary>
    List<Guid> CurrentUserRoleIds { get; set; }
}
