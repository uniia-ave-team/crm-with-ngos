using Crm.Application.Extensions;
using Crm.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace Crm.Api.Tests.Extensions;

/// <summary>
/// Verifies HTTP status code mapping and client/server classification in <see cref="ExceptionExtensions"/> without database provider dependencies.
/// </summary>
public class ExceptionExtensionsTests
{
    [Fact]
    public void MapToStatusCodeShouldReturn409ForEntityAlreadyExistsException()
    {
        var exception = new EntityAlreadyExistsException("Tag", "urgent");

        exception.MapToStatusCode().ShouldBe(StatusCodes.Status409Conflict);
        exception.IsClientError().ShouldBeTrue();
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
        exception.IsClientError().ShouldBeTrue();
    }

    [Fact]
    public void MapToStatusCodeShouldReturn409ForConcurrencyConflictException()
    {
        var exception = new ConcurrencyConflictException("Concurrent modification.");

        exception.MapToStatusCode().ShouldBe(StatusCodes.Status409Conflict);
        exception.IsClientError().ShouldBeTrue();
    }

    [Fact]
    public void MapToStatusCodeShouldReturn413ForStorageQuotaExceededException()
    {
        var exception = new StorageQuotaExceededException("Storage quota exceeded.");

        exception.MapToStatusCode().ShouldBe(StatusCodes.Status413PayloadTooLarge);
        exception.IsClientError().ShouldBeTrue();
    }

    [Fact]
    public void MapToStatusCodeShouldReturn500ForUntranslatedException()
    {
        var exception = new TimeoutException("Database timeout");

        exception.MapToStatusCode().ShouldBe(StatusCodes.Status500InternalServerError);
        exception.IsClientError().ShouldBeFalse();
    }
}
