using System.Collections.Frozen;

namespace Crm.Domain.Constants;

/// <summary>
/// Provides constant values related to logical file storage buckets and permitted image extensions.
/// </summary>
public static class FileStorageConstants
{
    /// <summary>
    /// The folder name used for storing User-related files, such as avatars.
    /// </summary>
    public const string UsersFolder = "users";

    /// <summary>
    /// The folder name used for storing NGO-related files, such as logos.
    /// </summary>
    public const string NgosFolder = "ngos";

    /// <summary>
    /// The folder name used for storing login page images and backgrounds.
    /// </summary>
    public const string LoginPageImagesFolder = "login-page-images";

    /// <summary>
    /// The folder name used for storing task file attachments.
    /// </summary>
    public const string TaskAttachmentsFolder = "task-attachments";

    /// <summary>
    /// Gets the immutable, case-insensitive allow-list of file extensions permitted for uploaded images and media.
    /// </summary>
    public static readonly FrozenSet<string> AllowedExtensions =
        new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".svg" }
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
