using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.LoginPageImage.Commands;

/// <summary>
/// Represents a command to upload a new login page image file via IFileStorageService
/// and create its database record.
/// </summary>
/// <param name="ContentStream">The stream containing the file data to be uploaded.</param>
/// <param name="OriginalFileName">The original name of the file, used to safely extract the extension.</param>
public record UploadLoginPageImageCommand(
    Stream ContentStream,
    string OriginalFileName) : IRequest<Guid>, ITransactionalCommand;
