namespace Crm.Domain.Constants;

/// <summary>
/// Provides standard binary byte multipliers for file size and payload limit calculations.
/// </summary>
public static class FileSizeUnits
{
    /// <summary>
    /// The number of bytes in one mebibyte (1,048,576 bytes).
    /// </summary>
    public const long Megabyte = 1024 * 1024;

    /// <summary>
    /// The number of bytes in one gibibyte (1,073,741,824 bytes).
    /// </summary>
    public const long Gigabyte = 1024L * Megabyte;
}
