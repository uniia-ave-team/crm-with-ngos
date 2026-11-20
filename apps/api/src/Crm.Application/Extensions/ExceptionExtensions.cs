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
    public static int MapToStatusCode(this Exception exception) => exception switch
    {
        // TODO: Remove InvalidOperationException, ArgumentNullException, FileNotFoundException, ArgumentException
        ValidationException or
        ArgumentException or
        InvalidOperationException or
        DomainValidationException or
        ArgumentNullException => (int)HttpStatusCode.BadRequest,
        InvalidCredentialException => (int)HttpStatusCode.Unauthorized,
        UnauthorizedAccessException => (int)HttpStatusCode.Forbidden,
        NotFoundException or FileNotFoundException => (int)HttpStatusCode.NotFound,
        EntityAlreadyExistsException or ConflictException => (int)HttpStatusCode.Conflict,
        StorageQuotaExceededException => (int)HttpStatusCode.RequestEntityTooLarge,
        _ => (int)HttpStatusCode.InternalServerError,
    };

    /// <summary>
    /// Determines whether the specified exception represents an expected client or domain error (HTTP 4xx)
    /// rather than an unhandled server failure (HTTP 5xx).
    /// </summary>
    /// <param name="exception">The exception instance to evaluate.</param>
    /// <returns><see langword="true"/> if the exception maps to a 4xx status code; otherwise, <see langword="false"/>.</returns>
    public static bool IsClientError(this Exception exception) =>
        exception.MapToStatusCode() < (int)HttpStatusCode.InternalServerError;
}
