using System.Security.Authentication;
using Crm.Application.Enums;
using Crm.Domain.Exceptions;
using FluentValidation;

namespace Crm.Application.Extensions;

/// <summary>
/// Provides the single source of truth for classifying domain, validation, and system exceptions
/// without coupling the Application layer to HTTP transport types.
/// </summary>
public static class ExceptionExtensions
{
    /// <summary>
    /// Determines whether the specified exception represents an expected client-originated error
    /// rather than an unhandled server failure.
    /// </summary>
    /// <param name="exception">The exception instance to evaluate.</param>
    /// <returns><see langword="true"/> if the exception represents a client error; otherwise, <see langword="false"/>.</returns>
    public static bool IsClientError(this Exception exception) =>
        exception.Classify() != ExceptionCategory.InternalError;

    /// <summary>
    /// Classifies an exception into its semantic <see cref="ExceptionCategory"/>.
    /// Register all new domain and application exception mappings here so HTTP status mapping
    /// and logging behavior remain synchronized from one place.
    /// Unregistered BCL exceptions (such as <see cref="InvalidOperationException"/>,
    /// <see cref="ArgumentException"/>, <see cref="ArgumentNullException"/>, or <see cref="FileNotFoundException"/>)
    /// intentionally fall through to <see cref="ExceptionCategory.InternalError"/> so library or programming bugs
    /// surface as unhandled 500 errors with full stack traces.
    /// </summary>
    /// <param name="exception">The exception instance to classify.</param>
    /// <returns>The semantic <see cref="ExceptionCategory"/> describing the exception.</returns>
    public static ExceptionCategory Classify(this Exception exception) => exception switch
    {
        ValidationException or DomainValidationException => ExceptionCategory.Validation,
        InvalidCredentialException => ExceptionCategory.Unauthorized,
        UnauthorizedAccessException => ExceptionCategory.Forbidden,
        NotFoundException => ExceptionCategory.NotFound,
        ConflictException => ExceptionCategory.Conflict,
        StorageQuotaExceededException => ExceptionCategory.PayloadTooLarge,
        _ => ExceptionCategory.InternalError,
    };
}
