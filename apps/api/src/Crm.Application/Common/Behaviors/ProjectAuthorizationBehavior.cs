using Crm.Application.Common.Interfaces;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Interfaces.Repositories;
using Mediator;

namespace Crm.Application.Common.Behaviors;

/// <summary>
/// Mediator pipeline behavior that centralizes project-level authorization and optional task-in-project
/// existence checks for all requests implementing <see cref="IProjectScoped"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the project-scoped request being handled.</typeparam>
/// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
/// <param name="projectAccessService">The scoped service resolving and caching project access metadata.</param>
/// <param name="taskRepository">The repository for verifying task existence in a project when <see cref="ITaskScoped"/> is implemented.</param>
public sealed class ProjectAuthorizationBehavior<TRequest, TResponse>(
    IProjectAccessService projectAccessService,
    IProjectTaskRepository taskRepository) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IProjectScoped, IMessage
{
    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TRequest message,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (message.ProjectId is not { } projectId)
        {
            return await next(message, cancellationToken);
        }

        ProjectAccess access = await projectAccessService.GetRequiredAsync(projectId, cancellationToken);
        access.EnsureHasClaim(projectId, message.RequiredProjectClaim);

        if (message is ITaskScoped taskScoped)
        {
            await taskRepository.EnsureExistsInProjectAsync(projectId, taskScoped.TaskId, cancellationToken);
        }

        return await next(message, cancellationToken);
    }
}
