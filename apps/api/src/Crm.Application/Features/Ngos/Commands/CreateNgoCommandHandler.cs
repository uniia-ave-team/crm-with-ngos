using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Ngo.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Ngos.Commands;

/// <summary>
/// Handles the <see cref="CreateNgoCommand"/> to create a new NGO entity and link all unassigned users to it.
/// </summary>
/// <param name="ngoRepository">The repository used for accessing and persisting NGO data.</param>
/// <param name="userRepository">The repository used for accessing and updating user domain profiles.</param>
/// <param name="unitOfWork">The unit of work for managing database transactions.</param>
/// <param name="timeProvider">The time provider for obtaining the current UTC time.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public partial class CreateNgoCommandHandler(
    INgoRepository ngoRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CreateNgoCommandHandler> logger) : IRequestHandler<CreateNgoCommand, Guid>
{
    public async Task<Guid> Handle(CreateNgoCommand request, CancellationToken cancellationToken)
    {
        LogCreatingNgo(logger, request.Name);

        await ngoRepository.EnsureDoesNotExistAsync(cancellationToken);

        var ngo = request.Adapt<Ngo>();

        ngo.CreatedAt = timeProvider.GetUtcNow().UtcDateTime;

        await ngoRepository.CreateAsync(ngo, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var unassignedUsersCount = await userRepository.AssignUnassignedUsersToNgoAsync(ngo.Id, cancellationToken);

        LogNgoCreatedSuccessfully(logger, ngo.Id, unassignedUsersCount);

        return ngo.Id;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingNgo, Level = LogLevel.Information, Message = "Initiating creation of NGO: {NgoName}")]
    private static partial void LogCreatingNgo(ILogger logger, string ngoName);

    [LoggerMessage(EventId = LogEventIds.NgoCreatedSuccessfully, Level = LogLevel.Information, Message = "NGO successfully created with ID: {NgoId} and linked to {UserCount} unassigned user profile(s).")]
    private static partial void LogNgoCreatedSuccessfully(ILogger logger, Guid ngoId, int userCount);
}
