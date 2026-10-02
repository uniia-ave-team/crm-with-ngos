using System.ComponentModel.DataAnnotations;

namespace Crm.Infrastructure.Options;

public class SerilogOptions
{
    public const string Position = "SerilogOptions";

    [Required(ErrorMessage = "LogPath is required.")]
    public string LogPath { get; set; } = "Logs";

    [Required(ErrorMessage = "AllLogsFileName is required.")]
    public string AllLogsFileName { get; set; } = "all-.log";

    [Required(ErrorMessage = "ErrorLogsFileName is required.")]
    public string ErrorLogsFileName { get; set; } = "errors-.log";

    [Range(1, 365, ErrorMessage = "RetainedFileCountLimit must be between 1 and 365.")]
    public int RetainedFileCountLimit { get; set; } = 5;

    [Range(1, 365, ErrorMessage = "RetainedErrorFileCountLimit must be between 1 and 365.")]
    public int RetainedErrorFileCountLimit { get; set; } = 31;
}
