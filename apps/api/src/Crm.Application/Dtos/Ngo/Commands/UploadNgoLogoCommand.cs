using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.Ngo.Commands;

/// <summary>
/// Represents a command to upload and update the logo for the single NGO in the system.
/// The handler for this command should delete the old logo, save the new one via IFileStorageService,
/// and update the NGO record in the database.
/// </summary>
/// <param name="ContentStream">The stream containing the file data to be uploaded.</param>
/// <param name="OriginalFileName">The original name of the file, used to safely extract the extension.</param>
public record UploadNgoLogoCommand(
    Stream ContentStream,
    string OriginalFileName) : IRequest, ITransactionalCommand;
