namespace Crm.Application.Common.Interfaces;

/// <summary>
/// Marker interface for requests that require the currently authenticated user's identifier,
/// automatically populated by <see cref="Behaviors.CurrentUserContextBehavior{TRequest, TResponse}"/>.
/// </summary>
public interface ICurrentUserAware : ICurrentUserContextRequest
{
    /// <summary>
    /// Gets or sets the unique identifier of the currently authenticated user.
    /// </summary>
    Guid CurrentUserId { get; set; }
}
