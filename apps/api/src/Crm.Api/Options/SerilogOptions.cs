using System.ComponentModel.DataAnnotations;

namespace Crm.Api.Options;

/// <summary>
/// Represents configuration options for Serilog file logging paths and retention policies.
/// </summary>
public sealed class SerilogOptions
{
    /// <summary>
    /// The configuration section name used to bind Serilog options.
    /// </summary>
    public const string Position = "SerilogOptions";

    /// <summary>
    /// Gets or sets the relative or absolute directory where log files are written.
    /// </summary>
    [Required(ErrorMessage = "LogPath is required.")]
    public string LogPath { get; set; } = "Logs";

    /// <summary>
    /// Gets or sets the rolling file name pattern for all application logs.
    /// </summary>
    [Required(ErrorMessage = "AllLogsFileName is required.")]
    public string AllLogsFileName { get; set; } = "all-.log";

    /// <summary>
    /// Gets or sets the rolling file name pattern for error-level logs.
    /// </summary>
    [Required(ErrorMessage = "ErrorLogsFileName is required.")]
    public string ErrorLogsFileName { get; set; } = "errors-.log";

    /// <summary>
    /// Gets or sets the maximum number of retained general log files.
    /// </summary>
    [Range(1, 365, ErrorMessage = "RetainedFileCountLimit must be between 1 and 365.")]
    public int RetainedFileCountLimit { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of retained error log files.
    /// </summary>
    [Range(1, 365, ErrorMessage = "RetainedErrorFileCountLimit must be between 1 and 365.")]
    public int RetainedErrorFileCountLimit { get; set; } = 31;
}
