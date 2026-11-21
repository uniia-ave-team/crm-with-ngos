using Crm.Domain.Enums;

namespace Crm.Application.Common.Interfaces;

/// <summary>
/// Marker interface for requests scoped to a project that require project-level authorization
/// via <see cref="Behaviors.ProjectAuthorizationBehavior{TRequest, TResponse}"/>.
/// </summary>
public interface IProjectScoped
{
    /// <summary>
    /// Gets the unique identifier of the target project, or <c>null</c> for global cross-project queries.
    /// </summary>
    Guid? ProjectId { get; }

    /// <summary>
    /// Gets the minimum project-level permission claim required to execute this request.
    /// </summary>
    ProjectClaim RequiredProjectClaim { get; }
}
