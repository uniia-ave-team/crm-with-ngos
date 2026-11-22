using System.ComponentModel.DataAnnotations;
using Crm.Domain.Constants;

namespace Crm.Infrastructure.Options;

/// <summary>
/// Configuration options controlling local disk file storage location, public base URL, and installation quota.
/// </summary>
public sealed class FileStorageOptions
{
    /// <summary>
    /// The configuration section name used to bind these options from the settings file.
    /// </summary>
    public const string Position = "FileStorage";

    /// <summary>
    /// The default maximum total file storage quota per installation (100 GB).
    /// </summary>
    public const long DefaultMaxTotalBytes = 100L * FileSizeUnits.Gigabyte;

    /// <summary>
    /// Gets or sets the relative or absolute path to the directory where uploaded files are saved.
    /// </summary>
    [Required(ErrorMessage = "Upload directory is required")]
    [MinLength(1, ErrorMessage = "Upload directory cannot be empty")]
    public string UploadDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base URL used to construct the full public URL for accessing the files.
    /// </summary>
    [Required(ErrorMessage = "Base URL is required")]
    [Url(ErrorMessage = "Base URL must be a valid URL")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum total size in bytes allowed across all uploaded files in the installation.
    /// Defaults to 100 GB.
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "Max total bytes must be greater than zero")]
    public long MaxTotalBytes { get; set; } = DefaultMaxTotalBytes;
}
