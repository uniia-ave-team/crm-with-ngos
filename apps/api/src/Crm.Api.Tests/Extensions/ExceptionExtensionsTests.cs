using Crm.Api.Extensions;
using Crm.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace Crm.Api.Tests.Extensions;

/// <summary>
/// Verifies HTTP status code mapping in <see cref="ExceptionExtensions"/> without database provider dependencies.
/// </summary>
public class ExceptionExtensionsTests
{
    [Fact]
    public void MapToStatusCodeShouldReturn409ForEntityAlreadyExistsException()
    {
        var exception = new EntityAlreadyExistsException("Tag", "urgent");

        exception.MapToStatusCode().ShouldBe(StatusCodes.Status409Conflict);
    }

    [Theory]
    [InlineData(ConflictKind.General)]
    [InlineData(ConflictKind.UniqueViolation)]
    [InlineData(ConflictKind.ReferenceViolation)]
    [InlineData(ConflictKind.Concurrency)]
    public void MapToStatusCodeShouldReturn409ForConflictExceptionKinds(ConflictKind kind)
    {
        var exception = new ConflictException("Conflict occurred.", kind);

        exception.MapToStatusCode().ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void MapToStatusCodeShouldReturn409ForConcurrencyConflictException()
    {
        var exception = new ConcurrencyConflictException("Concurrent modification.");

        exception.MapToStatusCode().ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void MapToStatusCodeShouldReturn500ForUntranslatedException()
    {
        var exception = new TimeoutException("Database timeout");

        exception.MapToStatusCode().ShouldBe(StatusCodes.Status500InternalServerError);
    }
}
