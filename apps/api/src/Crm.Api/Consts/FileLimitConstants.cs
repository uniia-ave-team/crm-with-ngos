using Crm.Domain.Consts;
using Crm.Domain.Consts.Entities.ValidationConstants;

namespace Crm.Api.Consts;

/// <summary>
/// Provides constant values for maximum file upload sizes and request limits in bytes.
/// </summary>
public static class FileLimitConstants
{
    /// <summary>
    /// Maximum allowed size for uploaded image files (avatars, logos, login images), set to 5 MB.
    /// </summary>
    public const long MaxImageUploadSize = 5 * FileSizeUnits.Megabyte;

    /// <summary>
    /// Additional request body overhead reserved for multipart form-data boundaries and headers (1 MB).
    /// </summary>
    public const long MultipartOverheadBytes = FileSizeUnits.Megabyte;

    /// <summary>
    /// Maximum HTTP request body size for multipart document uploads
    /// (<see cref="TaskValidationConstants.MaxAttachmentFileSize"/> plus multipart overhead).
    /// </summary>
    public const long MaxDocumentMultipartRequestSize = TaskValidationConstants.MaxAttachmentFileSize + MultipartOverheadBytes;
}
