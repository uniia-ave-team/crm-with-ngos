namespace Crm.Api.Consts;

/// <summary>
/// Provides constant values for maximum file upload sizes and request limits in bytes.
/// </summary>
public static class FileLimitConstants
{
    /// <summary>
    /// Maximum allowed size for uploaded image files (avatars, logos, login images), set to 5 MB.
    /// </summary>
    public const long MaxImageUploadSize = 5 * OneMegaByte;

    /// <summary>
    /// Maximum allowed size for general document files, set to 10 MB.
    /// </summary>
    public const long MaxDocumentUploadSize = 10 * OneMegaByte;

    private const int OneMegaByte = 1024 * 1024;
}
