using System.ComponentModel.DataAnnotations;

namespace Crm.Infrastructure.Options;

public class FileStorageOptions
{
    /// <summary>
    /// The configuration section name used to bind these options from the settings file.
    /// </summary>
    public const string Position = "FileStorage";

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
}
