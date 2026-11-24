using Crm.Application.Enums;
using Crm.Application.Extensions;

namespace Crm.Api.Extensions;

/// <summary>
/// Provides extension methods for mapping classified exceptions to HTTP status codes.
/// Concrete exception registration lives exclusively in <see cref="ExceptionExtensions.Classify"/>.
/// </summary>
public static class ExceptionHttpExtensions
{
    /// <summary>
    /// Maps a given exception instance to its corresponding HTTP status code integer
    /// using the semantic classification from <see cref="ExceptionExtensions.Classify"/>.
    /// </summary>
    /// <param name="exception">The exception instance to evaluate.</param>
    /// <returns>An HTTP status code (e.g., 400, 401, 403, 404, 409, 413, 500).</returns>
    public static int MapToStatusCode(this Exception exception) => exception.Classify() switch
    {
        ExceptionCategory.InternalError => StatusCodes.Status500InternalServerError,
        ExceptionCategory.Validation => StatusCodes.Status400BadRequest,
        ExceptionCategory.Unauthorized => StatusCodes.Status401Unauthorized,
        ExceptionCategory.Forbidden => StatusCodes.Status403Forbidden,
        ExceptionCategory.NotFound => StatusCodes.Status404NotFound,
        ExceptionCategory.Conflict => StatusCodes.Status409Conflict,
        ExceptionCategory.PayloadTooLarge => StatusCodes.Status413PayloadTooLarge,
        _ => StatusCodes.Status500InternalServerError,
    };
}
