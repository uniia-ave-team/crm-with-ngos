namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when a file upload exceeds a task attachment quota or the installation-wide file storage quota.
/// </summary>
/// <param name="message">The human-readable description of the exceeded storage limit.</param>
public class StorageQuotaExceededException(string message)
    : Exception(message);
