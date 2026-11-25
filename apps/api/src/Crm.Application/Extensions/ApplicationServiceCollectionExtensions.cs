using Crm.Application.Common.Behaviors;
using Crm.Application.Common.Security;
using Crm.Application.Features.Tasks.Services;
using Crm.Application.Interfaces;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Application.Extensions;

/// <summary>
/// Provides extension methods for registering application-layer services
/// in the dependency injection container.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers application services including Mediator handlers, pipeline behaviors,
    /// project access service, task assignee validator, and FluentValidation validators from the application assembly.
    /// </summary>
    /// <remarks>
    /// Pipeline behaviors are executed in the order they are registered:
    /// <list type="number">
    /// <item><description><see cref="CurrentUserContextBehavior{TRequest, TResponse}"/> — populates authenticated user ID and role IDs from verified claims first.</description></item>
    /// <item><description><see cref="ProjectAuthorizationBehavior{TRequest, TResponse}"/> — enforces project-level permissions and task scoping.</description></item>
    /// <item><description><see cref="ValidationBehavior{TRequest, TResponse}"/> — validates authorized requests before execution.</description></item>
    /// <item><description><see cref="TransactionalBehavior{TRequest, TResponse}"/> — handles database transactions for valid, authorized requests.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IProjectAccessService, ProjectAccessService>();
        services.AddScoped<ITaskAssigneeValidator, TaskAssigneeValidator>();

        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors =
            [
                typeof(CurrentUserContextBehavior<,>),
                typeof(ProjectAuthorizationBehavior<,>),
                typeof(ValidationBehavior<,>),
                typeof(TransactionalBehavior<,>),
            ];
        });

        services.AddValidatorsFromAssembly(typeof(ApplicationServiceCollectionExtensions).Assembly);

        return services;
    }
}
