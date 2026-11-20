using System.Net;
using System.Security.Authentication;
using Crm.Domain.Exceptions;
using FluentValidation;

namespace Crm.Application.Extensions;

/// <summary>
/// Provides extension methods for classifying domain and system exceptions and mapping them to HTTP status codes.
/// </summary>
public static class ExceptionExtensions
{
    /// <summary>
    /// Maps a given exception instance to its corresponding HTTP status code integer.
    /// </summary>
    /// <param name="exception">The exception instance to evaluate.</param>
    /// <returns>An HTTP status code (e.g., 400, 401, 403, 404, 409, 413, 500).</returns>
    public static int MapToStatusCode(this Exception exception) =>
        (int)Classify(exception).StatusCode;

    /// <summary>
    /// Determines whether the specified exception represents a client error (HTTP 4xx)
    /// rather than an unhandled server failure (HTTP 5xx).
    /// </summary>
    /// <param name="exception">The exception instance to evaluate.</param>
    /// <returns><see langword="true"/> if the exception maps to a 4xx status code; otherwise, <see langword="false"/>.</returns>
    public static bool IsClientError(this Exception exception) =>
        Classify(exception).StatusCode < HttpStatusCode.InternalServerError;

    /// <summary>
    /// Determines whether the exception is an explicit domain, validation, or access-control error whose type and
    /// message are sufficient for diagnostics without logging a full stack trace. Generic BCL exceptions (such as
    /// <see cref="InvalidOperationException"/> or <see cref="ArgumentException"/>) return <see langword="false"/>
    /// so unexpected programming bugs keep their stack traces in logs.
    /// </summary>
    /// <param name="exception">The exception instance to evaluate.</param>
    /// <returns><see langword="true"/> for explicit domain/validation exceptions; otherwise, <see langword="false"/>.</returns>
    public static bool IsExpectedDomainError(this Exception exception) =>
        Classify(exception).IsExpectedDomainError;

    private static (HttpStatusCode StatusCode, bool IsExpectedDomainError) Classify(Exception exception) => exception switch
    {
        ObjectDisposedException => (HttpStatusCode.InternalServerError, false),
        ValidationException or DomainValidationException => (HttpStatusCode.BadRequest, true),
        InvalidCredentialException => (HttpStatusCode.Unauthorized, true),
        UnauthorizedAccessException => (HttpStatusCode.Forbidden, true),
        NotFoundException => (HttpStatusCode.NotFound, true),
        EntityAlreadyExistsException or ConflictException => (HttpStatusCode.Conflict, true),
        StorageQuotaExceededException => (HttpStatusCode.RequestEntityTooLarge, true),

        // TODO: Remove InvalidOperationException, ArgumentNullException, FileNotFoundException, ArgumentException
        ArgumentNullException or ArgumentException or InvalidOperationException => (HttpStatusCode.BadRequest, false),
        FileNotFoundException => (HttpStatusCode.NotFound, false),

        _ => (HttpStatusCode.InternalServerError, false),
    };
}
