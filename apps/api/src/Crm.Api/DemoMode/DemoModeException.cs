namespace Crm.Api.DemoMode;

/// <summary>
/// An error specific to demo mode. It is reported to the client as a problem response
/// with a machine-readable <c>errorCode</c>.
/// </summary>
public sealed class DemoModeException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DemoModeException"/> class.
    /// </summary>
    public DemoModeException()
        : this(DemoErrorCodes.Restricted, StatusCodes.Status403Forbidden, DemoErrorCodes.RestrictedMessage)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoModeException"/> class.
    /// </summary>
    /// <param name="message">The message shown to the client.</param>
    public DemoModeException(string message)
        : this(DemoErrorCodes.Restricted, StatusCodes.Status403Forbidden, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoModeException"/> class.
    /// </summary>
    /// <param name="message">The message shown to the client.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public DemoModeException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = DemoErrorCodes.Restricted;
        StatusCode = StatusCodes.Status403Forbidden;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoModeException"/> class.
    /// </summary>
    /// <param name="errorCode">One of <see cref="DemoErrorCodes"/>.</param>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    /// <param name="message">The message shown to the client.</param>
    public DemoModeException(string errorCode, int statusCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the machine-readable error code, one of <see cref="DemoErrorCodes"/>.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the HTTP status code of the response.
    /// </summary>
    public int StatusCode { get; }
}
