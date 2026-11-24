using System.Reflection;
using Crm.Api.Controllers.V1;
using Crm.Application.Common.Interfaces;
using Crm.Application.Extensions;
using Crm.Application.Features.Projects.Commands;
using Crm.Application.Features.Projects.Queries;
using Crm.Application.Features.Tasks.Commands;
using Crm.Application.Features.Tasks.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Infrastructure.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;
using NetArchTest.Rules;
using Npgsql;

namespace Crm.Architecture.Tests;

/// <summary>
/// Enforces clean-architecture layer boundaries and dependency rules across Domain, Application, Infrastructure, and Api.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(ProjectTask).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ApplicationServiceCollectionExtensions).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(ApplicationDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(ProjectsController).Assembly;

    [Fact]
    public void DomainShouldNotDependOnUpperLayersOrPersistenceProviders()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Crm.Application",
                "Crm.Infrastructure",
                "Crm.Api",
                "Microsoft.EntityFrameworkCore",
                "Npgsql")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void ApplicationShouldNotDependOnInfrastructureApiOrPersistenceProviders()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Crm.Infrastructure",
                "Crm.Api",
                "Microsoft.EntityFrameworkCore",
                "Npgsql")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void ApplicationHandlersShouldNotDependDirectlyOnDbContext()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(IRequestHandler<,>))
            .Or()
            .ImplementInterface(typeof(IRequestHandler<>))
            .Or()
            .ImplementInterface(typeof(ICommandHandler<,>))
            .Or()
            .ImplementInterface(typeof(ICommandHandler<>))
            .Or()
            .ImplementInterface(typeof(IQueryHandler<,>))
            .ShouldNot()
            .HaveDependencyOn(typeof(DbContext).Namespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void ApplicationHandlersShouldNotDependOnCurrentUserService()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(IRequestHandler<,>))
            .Or()
            .ImplementInterface(typeof(IRequestHandler<>))
            .Or()
            .ImplementInterface(typeof(ICommandHandler<,>))
            .Or()
            .ImplementInterface(typeof(ICommandHandler<>))
            .Or()
            .ImplementInterface(typeof(IQueryHandler<,>))
            .ShouldNot()
            .HaveDependencyOn(typeof(ICurrentUserService).FullName)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void ApiExceptionExtensionsAndMiddlewareShouldNotDependOnNpgsqlOrEntityFrameworkCore()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace("Crm.Api.Extensions")
            .Or()
            .ResideInNamespace("Crm.Api.Middleware")
            .ShouldNot()
            .HaveDependencyOnAny(
                typeof(NpgsqlException).Namespace,
                typeof(DbContext).Namespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void GenericRepositoryShouldNotDependOnNpgsql()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That()
            .HaveNameStartingWith("GenericRepository")
            .ShouldNot()
            .HaveDependencyOn(typeof(NpgsqlException).Namespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void ProjectAndTaskRequestsShouldImplementProjectScopedAuthorization()
    {
        HashSet<Type> allowedUnscopedRequests =
        [
            typeof(CreateProjectCommand),
            typeof(GetProjectsQuery),
        ];

        List<Type> violatingTypes = [
            .. ApplicationAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && t.Namespace is not null
                        && (t.Namespace.StartsWith("Crm.Application.Features.Projects", StringComparison.Ordinal)
                            || t.Namespace.StartsWith("Crm.Application.Features.Tasks", StringComparison.Ordinal))
                        && IsMediatorRequest(t)
                        && !allowedUnscopedRequests.Contains(t)
                        && !typeof(IProjectScoped).IsAssignableFrom(t)),
        ];

        Assert.True(
            violatingTypes.Count == 0,
            $"Project/task requests missing IProjectScoped: {string.Join(", ", violatingTypes.Select(t => t.FullName))}");
    }

    [Fact]
    public void TaskSubResourceRequestsShouldImplementTaskScopedAuthorization()
    {
        HashSet<Type> directTaskRequests =
        [
            typeof(GetTaskByIdQuery),
            typeof(UpdateTaskCommand),
            typeof(UpdateTaskStatusCommand),
            typeof(DeleteTaskCommand),
        ];

        List<Type> violatingTypes = [
            .. ApplicationAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && t.Namespace is not null
                        && t.Namespace.StartsWith("Crm.Application.Features.Tasks", StringComparison.Ordinal)
                        && IsMediatorRequest(t)
                        && t.GetProperty("TaskId") is not null
                        && !directTaskRequests.Contains(t)
                        && !typeof(ITaskScoped).IsAssignableFrom(t)),
        ];

        Assert.True(
            violatingTypes.Count == 0,
            $"Task sub-resource requests missing ITaskScoped: {string.Join(", ", violatingTypes.Select(t => t.FullName))}");
    }

    private static bool IsMediatorRequest(Type type)
        => type.GetInterfaces().Any(i =>
            i == typeof(IRequest)
            || i == typeof(ICommand)
            || (i.IsGenericType && (
                i.GetGenericTypeDefinition() == typeof(IRequest<>)
                || i.GetGenericTypeDefinition() == typeof(ICommand<>)
                || i.GetGenericTypeDefinition() == typeof(IQuery<>))));

    private static string FormatFailingTypes(NetArchTest.Rules.TestResult result)
        => result.FailingTypeNames is { Count: > 0 }
            ? $"Violating types: {string.Join(", ", result.FailingTypeNames)}"
            : string.Empty;
}
