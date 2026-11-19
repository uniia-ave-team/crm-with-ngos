namespace Crm.Domain.Consts;

/// <summary>
/// Provides standard binary byte multipliers for file size and payload limit calculations.
/// </summary>
public static class FileSizeUnits
{
    /// <summary>
    /// The number of bytes in one kibibyte (1,024 bytes).
    /// </summary>
    public const long Kilobyte = 1024;

    /// <summary>
    /// The number of bytes in one mebibyte (1,048,576 bytes).
    /// </summary>
    public const long Megabyte = 1024 * 1024;
}
