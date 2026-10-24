namespace Crm.Domain.Consts;

/// <summary>
/// Provides constant values related to file storage mechanisms and directory structure across the application.
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
    /// The list of allowed file extensions for uploaded images and media.
    /// </summary>
    public static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".svg"];
}
