namespace Crm.Application.Common.Interfaces;

/// <summary>
/// Marker interface for commands and queries targeting a single entity by its <see cref="Id"/>,
/// automatically validated for non-empty GUIDs in <see cref="Behaviors.ValidationBehavior{TRequest, TResponse}"/>.
/// </summary>
public interface IHasEntityId
{
    /// <summary>
    /// Gets the unique identifier of the target entity.
    /// </summary>
    Guid Id { get; }
}
