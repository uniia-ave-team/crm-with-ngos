namespace Crm.Infrastructure.Consts;

/// <summary>
/// Provides infrastructure constants for local disk staging and orphaned temporary file cleanup.
/// </summary>
public static class LocalFileStorageConstants
{
    /// <summary>
    /// The file extension used for temporary staging files during atomic uploads.
    /// </summary>
    public const string TempFileExtension = ".tmp";

    /// <summary>
    /// The interval in hours at which the orphaned temporary files cleanup process runs.
    /// </summary>
    public const int TempFileCleanupIntervalHours = 1;

    /// <summary>
    /// The maximum age in hours before an abandoned temporary upload file is considered stale and eligible for deletion.
    /// </summary>
    public const int StaleTempFileMaxAgeHours = 1;
}
