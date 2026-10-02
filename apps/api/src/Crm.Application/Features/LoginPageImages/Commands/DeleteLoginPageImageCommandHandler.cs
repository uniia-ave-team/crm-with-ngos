using Crm.Application.Common.Consts;
using Crm.Application.Dtos.LoginPageImage.Commands;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.LoginPageImages.Commands;

/// <summary>
/// Handles the <see cref="DeleteLoginPageImageCommand"/> to remove an existing login page image from the system.
/// </summary>
/// <param name="repository">The repository used to manage data access operations for login page images.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the login page image deletion process.</param>
public partial class DeleteLoginPageImageCommandHandler(
    ILoginPageImageRepository repository,
    ILogger<DeleteLoginPageImageCommandHandler> logger) : IRequestHandler<DeleteLoginPageImageCommand>
{
    public async Task Handle(DeleteLoginPageImageCommand request, CancellationToken cancellationToken)
    {
        LogDeletingImage(logger, request.Id);

        await repository.DeleteAsync(request.Id, cancellationToken);

        LogImageDeletedSuccessfully(logger, request.Id);
    }

    [LoggerMessage(EventId = LogEventIds.DeletingLoginPageImage, Level = LogLevel.Information, Message = "Initiating deletion of login page image with ID: {Id}")]
    private static partial void LogDeletingImage(ILogger logger, Guid id);

    [LoggerMessage(EventId = LogEventIds.LoginPageImageDeletedSuccessfully, Level = LogLevel.Information, Message = "Login page image with ID: {Id} successfully deleted.")]
    private static partial void LogImageDeletedSuccessfully(ILogger logger, Guid id);
}
