using System.Reflection;
using Crm.Api.Controllers.V1;
using Crm.Application.Extensions;
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
            .ShouldNot()
            .HaveDependencyOn(typeof(DbContext).Namespace)
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

    private static string FormatFailingTypes(NetArchTest.Rules.TestResult result)
        => result.FailingTypeNames is { Count: > 0 }
            ? $"Violating types: {string.Join(", ", result.FailingTypeNames)}"
            : string.Empty;
}
