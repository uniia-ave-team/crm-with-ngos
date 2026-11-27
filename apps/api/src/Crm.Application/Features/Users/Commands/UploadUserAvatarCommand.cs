using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Represents a command to upload and update the profile image for a specific user.
/// The handler for this command should delete the old image, save the new one via IFileStorageService,
/// and update the user record in the database.
/// </summary>
/// <param name="UserId">The unique identifier of the user whose image is being updated.</param>
/// <param name="ContentStream">The stream containing the file data to be uploaded.</param>
/// <param name="OriginalFileName">The original name of the file, used to safely extract the extension.</param>
/// <param name="TargetCurrentUser">Indicates whether to resolve <paramref name="UserId"/> from the currently authenticated user (<c>/me</c> route).</param>
public record UploadUserAvatarCommand(
    Guid UserId,
    Stream ContentStream,
    string OriginalFileName,
    bool TargetCurrentUser = false) : ICommand, ITransactionalCommand, ISelfTargetableUserRequest
{
    /// <inheritdoc />
    public Guid UserId { get; set; } = UserId;
}
