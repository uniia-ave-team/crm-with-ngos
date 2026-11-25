using Crm.Application.Common.Interfaces;
using Crm.Application.Interfaces;
using Mediator;

namespace Crm.Application.Common.Behaviors;

/// <summary>
/// Mediator pipeline behavior that automatically populates authenticated user identity context
/// (<see cref="ICurrentUserAware.CurrentUserId"/>, <see cref="ISelfTargetableUserRequest.UserId"/> when self-targeted,
/// and <see cref="ICurrentUserRolesAware.CurrentUserRoleIds"/>) from <see cref="ICurrentUserService"/>
/// before downstream authorization, validation, and handler execution.
/// Unconditionally overwrites the properties from the verified token claims to prevent query/body spoofing.
/// </summary>
/// <typeparam name="TRequest">The type of the request requiring current user context.</typeparam>
/// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
/// <param name="currentUserService">The service providing the currently authenticated user's identity claims.</param>
public sealed class CurrentUserContextBehavior<TRequest, TResponse>(
    ICurrentUserService currentUserService) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICurrentUserContextRequest, IMessage
{
    /// <inheritdoc />
    public ValueTask<TResponse> Handle(
        TRequest message,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        Guid? resolvedUserId = null;

        if (message is ICurrentUserAware userAware)
        {
            resolvedUserId = currentUserService.GetUserId();
            userAware.CurrentUserId = resolvedUserId.Value;
        }

        if (message is ISelfTargetableUserRequest { TargetCurrentUser: true } selfTarget)
        {
            selfTarget.UserId = resolvedUserId ?? currentUserService.GetUserId();
        }

        if (message is ICurrentUserRolesAware rolesAware)
        {
            rolesAware.CurrentUserRoleIds = currentUserService.GetRoleIds();
        }

        return next(message, cancellationToken);
    }
}
