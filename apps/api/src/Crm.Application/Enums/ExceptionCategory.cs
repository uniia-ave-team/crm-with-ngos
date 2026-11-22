namespace Crm.Application.Enums;

/// <summary>
/// Represents a transport-agnostic semantic category for exceptions handled across
/// the Application and Presentation layers without coupling Application logic to HTTP status codes.
/// </summary>
public enum ExceptionCategory
    : byte
{
    /// <summary>
    /// Unexpected internal error or infrastructure fault.
    /// </summary>
    InternalError = 0,

    /// <summary>
    /// Domain rule violation or input validation failure.
    /// </summary>
    Validation = 1,

    /// <summary>
    /// Authentication failure (missing or invalid credentials).
    /// </summary>
    Unauthorized = 2,

    /// <summary>
    /// Authorization or access-control failure.
    /// </summary>
    Forbidden = 3,

    /// <summary>
    /// Requested domain entity or resource was not found.
    /// </summary>
    NotFound = 4,

    /// <summary>
    /// State conflict such as duplicate entity, reference violation, or concurrency mismatch.
    /// </summary>
    Conflict = 5,

    /// <summary>
    /// Uploaded payload or storage quota exceeded configured limits.
    /// </summary>
    PayloadTooLarge = 6,
}
