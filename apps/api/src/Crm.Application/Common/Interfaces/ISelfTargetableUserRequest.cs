namespace Crm.Application.Common.Interfaces;

/// <summary>
/// Marker interface for user-targeted requests that support both explicit target routes
/// (e.g., <c>/users/{id}/...</c>) and self-service routes (e.g., <c>/users/me/...</c>).
/// When <see cref="TargetCurrentUser"/> is <see langword="true"/>,
/// <see cref="Behaviors.CurrentUserContextBehavior{TRequest, TResponse}"/> unconditionally sets
/// <see cref="UserId"/> to the currently authenticated user's identifier from verified claims.
/// </summary>
public interface ISelfTargetableUserRequest : ICurrentUserContextRequest
{
    /// <summary>
    /// Gets a value indicating whether the target <see cref="UserId"/> should be resolved
    /// from the currently authenticated user's verified claims.
    /// </summary>
    bool TargetCurrentUser { get; }

    /// <summary>
    /// Gets or sets the unique identifier of the target user.
    /// </summary>
    Guid UserId { get; set; }
}
