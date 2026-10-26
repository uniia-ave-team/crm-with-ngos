using System.Security.Authentication;
using Crm.Domain.Exceptions;
using FluentValidation;

namespace Crm.Api.Extensions;

/// <summary>
/// Provides extension methods for mapping domain and system exceptions to HTTP status codes.
/// </summary>
public static class ExceptionExtensions
{
    /// <summary>
    /// Maps a given exception instance to its corresponding HTTP status code integer.
    /// </summary>
    /// <param name="exception">The exception instance to evaluate.</param>
    /// <returns>An HTTP status code (e.g., 400, 401, 403, 500).</returns>
    public static int MapToStatusCode(this Exception exception) => exception switch
    {
        // TODO: Remove InvalidOperationException, ArgumentNullException, FileNotFoundException, ArgumentException
        ValidationException or
        ArgumentException or
        InvalidOperationException or
        DomainValidationException or
        ArgumentNullException => StatusCodes.Status400BadRequest,
        InvalidCredentialException => StatusCodes.Status401Unauthorized,
        UnauthorizedAccessException => StatusCodes.Status403Forbidden,
        NotFoundException or FileNotFoundException => StatusCodes.Status404NotFound,
        EntityAlreadyExistsException => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };
}
