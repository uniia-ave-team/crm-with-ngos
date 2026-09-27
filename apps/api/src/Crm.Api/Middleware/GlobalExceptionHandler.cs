using Crm.Api.Consts;
using Crm.Api.Extensions;
using Crm.Application.Common.Consts;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Middleware;

/// <summary>
/// Handles global exceptions and writes standardized Problem Details responses.
/// </summary>
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger,
    IWebHostEnvironment env)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        int statusCode = exception.MapToStatusCode();
        bool isClientError = statusCode < StatusCodes.Status500InternalServerError;

        httpContext.Response.StatusCode = statusCode;

        LogException(logger, exception, isClientError);

        ProblemDetails problemDetails = exception switch
        {
            ValidationException validationEx => CreateValidationProblemDetails(validationEx, statusCode),

            _ => new ProblemDetails
            {
                Status = statusCode,
                Title = isClientError ? ProblemDetailsDefaults.ClientErrorTitle : ProblemDetailsDefaults.ServerErrorTitle,
                Detail = GetDetailMessage(exception, isClientError, env.IsDevelopment()),
                Type = ProblemDetailsDefaults.GetTypeUrl(statusCode),
            },
        };

        problemDetails.Instance = httpContext.Request.Path;

        if (env.IsDevelopment() && exception is not ValidationException)
        {
            problemDetails.Extensions[ProblemDetailsDefaults.StackTraceExtensionKey] = exception.StackTrace;
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }

    /// <summary>
    /// Creates an <see cref="HttpValidationProblemDetails"/> instance without using reflection,
    /// relying strictly on the strongly-typed properties of FluentValidation.
    /// </summary>
    private static HttpValidationProblemDetails CreateValidationProblemDetails(ValidationException ex, int statusCode)
    {
        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return new HttpValidationProblemDetails(errors)
        {
            Status = statusCode,
            Title = ProblemDetailsDefaults.ValidationFailedTitle,
            Detail = ProblemDetailsDefaults.ValidationFailedDetail,
            Type = ProblemDetailsDefaults.GetTypeUrl(statusCode),
        };
    }

    /// <summary>
    /// Determines the safest detail message to expose to the client.
    /// </summary>
    private static string GetDetailMessage(Exception exception, bool isClientError, bool isDev) =>
        isDev || isClientError
            ? exception.Message
            : ProblemDetailsDefaults.UnexpectedErrorDetail;

    /// <summary>
    /// Routes the exception to the appropriate high-performance logger method.
    /// </summary>
    private static void LogException(ILogger logger, Exception exception, bool isClientError)
    {
        if (isClientError)
        {
            LogClientError(logger, exception, exception.GetType().Name, exception.Message);
        }
        else
        {
            LogServerError(logger, exception, exception.GetType().Name, exception.Message);
        }
    }

    [LoggerMessage(EventId = LogEventIds.ClientError, Level = LogLevel.Warning, Message = "Client error occurred: {ExceptionType} - {Message}")]
    private static partial void LogClientError(ILogger logger, Exception exception, string exceptionType, string message);

    [LoggerMessage(EventId = LogEventIds.ServerError, Level = LogLevel.Error, Message = "Unhandled server exception: {ExceptionType} - {Message}")]
    private static partial void LogServerError(ILogger logger, Exception exception, string exceptionType, string message);
}
