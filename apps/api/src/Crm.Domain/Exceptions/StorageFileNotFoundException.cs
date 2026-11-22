namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when a requested physical file does not exist in storage.
/// </summary>
/// <param name="fileName">The requested file name or storage identifier.</param>
/// <param name="folderName">The storage folder where the file was expected.</param>
public sealed class StorageFileNotFoundException(string fileName, string folderName)
    : NotFoundException($"The file '{fileName}' was not found in storage folder '{folderName}'.");
