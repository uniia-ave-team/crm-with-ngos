namespace Crm.Application.Common.Interfaces;

/// <summary>
/// Marker interface for requests scoped to an existing task within a project,
/// verifying task membership in the project before executing the handler.
/// </summary>
public interface ITaskScoped : IProjectScoped
{
    /// <summary>
    /// Gets the unique identifier of the target task within the project.
    /// </summary>
    Guid TaskId { get; }
}
