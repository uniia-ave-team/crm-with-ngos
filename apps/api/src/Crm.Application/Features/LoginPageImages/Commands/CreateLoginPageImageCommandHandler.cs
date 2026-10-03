using Crm.Application.Common.Consts;
using Crm.Application.Dtos.LoginPageImage.Commands;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.LoginPageImages.Commands;

/// <summary>
/// Handles the <see cref="CreateLoginPageImageCommand"/> to create a new login page image in the system.
/// </summary>
/// <param name="repository">The repository used to manage data access operations for login page images.</param>
/// <param name="ngoRepository">The repository used to retrieve the primary Non-Governmental Organization (NGO) associated with the application.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the login page image creation process.</param>
public partial class CreateLoginPageImageCommandHandler(
    ILoginPageImageRepository repository,
    INgoRepository ngoRepository,
    ILogger<CreateLoginPageImageCommandHandler> logger) : IRequestHandler<CreateLoginPageImageCommand, Guid>
{
    public async Task<Guid> Handle(CreateLoginPageImageCommand request, CancellationToken cancellationToken)
    {
        LogCreatingImage(logger, request.Url);

        var ngoId = await ngoRepository.GetIdAsync(cancellationToken);

        var loginPageImage = request.Adapt<LoginPageImage>();

        loginPageImage.NgoId = ngoId;

        await repository.CreateAsync(loginPageImage, cancellationToken);

        LogImageCreatedSuccessfully(logger, request.Url);

        return loginPageImage.Id;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingLoginPageImage, Level = LogLevel.Information, Message = "Initiating creation of login page image with URL: {Url}.")]
    private static partial void LogCreatingImage(ILogger logger, string url);

    [LoggerMessage(EventId = LogEventIds.LoginPageImageCreatedSuccessfully, Level = LogLevel.Information, Message = "Login page image '{Url}' successfully created.")]
    private static partial void LogImageCreatedSuccessfully(ILogger logger, string url);
}
