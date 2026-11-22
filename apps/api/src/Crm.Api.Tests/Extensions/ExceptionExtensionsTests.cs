using System.Security.Authentication;
using Crm.Api.Extensions;
using Crm.Application.Enums;
using Crm.Application.Extensions;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using FluentValidation;
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
    public void MapToStatusCodeShouldReturn500ForObjectDisposedExceptionAndNotClassifyAsClientError()
    {
        var exception = new ObjectDisposedException("MemoryStream");

        exception.MapToStatusCode().ShouldBe(StatusCodes.Status500InternalServerError);
        exception.IsClientError().ShouldBeFalse();
        exception.Classify().ShouldBe(ExceptionCategory.InternalError);
    }

    [Fact]
    public void GenericBclExceptionsShouldSurfaceAs500InternalServerErrors()
    {
        Exception[] bclExceptions =
        [
            new InvalidOperationException("Unexpected state"),
            new ArgumentException("Invalid argument"),
            new ArgumentNullException("param"),
            new FileNotFoundException("Missing library file"),
        ];

        foreach (var exception in bclExceptions)
        {
            exception.Classify().ShouldBe(ExceptionCategory.InternalError);
            exception.MapToStatusCode().ShouldBe(StatusCodes.Status500InternalServerError);
            exception.IsClientError().ShouldBeFalse();
        }
    }

    [Fact]
    public void MapToStatusCodeShouldReturn500ForUntranslatedException()
    {
        var exception = new TimeoutException("Database timeout");

        exception.MapToStatusCode().ShouldBe(StatusCodes.Status500InternalServerError);
        exception.IsClientError().ShouldBeFalse();
        exception.Classify().ShouldBe(ExceptionCategory.InternalError);
    }

    [Fact]
    public void ClassifyShouldMapRemainingCategoriesAndConsistentHttpCodes()
    {
        var validation = new ValidationException("Invalid input");
        validation.Classify().ShouldBe(ExceptionCategory.Validation);
        validation.MapToStatusCode().ShouldBe(StatusCodes.Status400BadRequest);
        validation.IsClientError().ShouldBeTrue();

        var domainValidation = new DomainValidationException("Domain validation failed");
        domainValidation.Classify().ShouldBe(ExceptionCategory.Validation);
        domainValidation.MapToStatusCode().ShouldBe(StatusCodes.Status400BadRequest);
        domainValidation.IsClientError().ShouldBeTrue();

        var unauthorized = new InvalidCredentialException("Invalid credentials");
        unauthorized.Classify().ShouldBe(ExceptionCategory.Unauthorized);
        unauthorized.MapToStatusCode().ShouldBe(StatusCodes.Status401Unauthorized);
        unauthorized.IsClientError().ShouldBeTrue();

        var forbidden = new UnauthorizedAccessException("Forbidden");
        forbidden.Classify().ShouldBe(ExceptionCategory.Forbidden);
        forbidden.MapToStatusCode().ShouldBe(StatusCodes.Status403Forbidden);
        forbidden.IsClientError().ShouldBeTrue();

        var notFound = new EntityNotFoundException<Domain.Entities.Project>(Guid.CreateVersion7());
        notFound.Classify().ShouldBe(ExceptionCategory.NotFound);
        notFound.MapToStatusCode().ShouldBe(StatusCodes.Status404NotFound);
        notFound.IsClientError().ShouldBeTrue();

        var storageNotFound = new StorageFileNotFoundException("missing.png", "users");
        storageNotFound.Classify().ShouldBe(ExceptionCategory.NotFound);
        storageNotFound.MapToStatusCode().ShouldBe(StatusCodes.Status404NotFound);
        storageNotFound.IsClientError().ShouldBeTrue();
    }
}
