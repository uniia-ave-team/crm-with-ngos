using Crm.Application.Common.Behaviors;
using FluentValidation;
using Mapster;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Application.Extensions;

/// <summary>
/// Provides extension methods for registering application-layer services
/// in the dependency injection container.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers application services including MediatR handlers, pipeline behaviors,
    /// and FluentValidation validators from the application assembly.
    /// </summary>
    /// <remarks>
    /// Pipeline behaviors are executed in the order they are registered:
    /// <list type="number">
    /// <item><description><see cref="ValidationBehavior{TRequest, TResponse}"/> — validates requests first (fail-fast).</description></item>
    /// <item><description><see cref="TransactionalBehavior{TRequest, TResponse}"/> — handles database transactions for valid requests.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ApplicationServiceCollectionExtensions).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionalBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(ApplicationServiceCollectionExtensions).Assembly);

        services.AddMapster();

        return services;
    }

    /// <summary>
    /// Scans the application assembly for <see cref="IRegister"/> implementations
    /// and configures Mapster global settings.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddMapster(this IServiceCollection services)
    {
        TypeAdapterConfig.GlobalSettings.Scan(typeof(ApplicationServiceCollectionExtensions).Assembly);

        return services;
    }
}
