using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.LoginPageImages.Commands;

/// <summary>
/// Represents a command to upload a new login page image file via IFileStorageService
/// and create its database record.
/// </summary>
/// <param name="ContentStream">The stream containing the file data to be uploaded.</param>
/// <param name="OriginalFileName">The original name of the file, used to safely extract the extension.</param>
public record UploadLoginPageImageCommand(
    Stream ContentStream,
    string OriginalFileName) : ICommand<Guid>, ITransactionalCommand;
