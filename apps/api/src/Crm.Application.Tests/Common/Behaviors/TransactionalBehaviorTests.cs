using Crm.Application.Common.Behaviors;
using Crm.Application.Common.Interfaces;
using Crm.Application.Interfaces;
using Mediator;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Application.Tests.Common.Behaviors;

/// <summary>
/// Unit tests for <see cref="TransactionalBehavior{TRequest, TResponse}"/> verifying normal commits,
/// exception rollbacks, and full transactional coverage of all application commands.
/// </summary>
public class TransactionalBehaviorTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileTransactionTracker _fileTracker = Substitute.For<IFileTransactionTracker>();

    [Fact]
    public async Task HandleWhenHandlerSucceedsShouldCommitTransactionAndPendingDeletions()
    {
        var ct = TestContext.Current.CancellationToken;
        var expectedId = Guid.NewGuid();

        var behavior = new TransactionalBehavior<RegularCommand, Guid>(
            _unitOfWork,
            _fileTracker,
            Substitute.For<ILogger<TransactionalBehavior<RegularCommand, Guid>>>());

        var result = await behavior.Handle(
            new RegularCommand(),
            (_, _) => ValueTask.FromResult(expectedId),
            ct);

        result.ShouldBe(expectedId);
        await _unitOfWork.Received(1).BeginTransactionAsync(ct);
        await _unitOfWork.Received(1).CommitTransactionAsync(ct);
        await _fileTracker.Received(1).CommitPendingDeletionsAsync(CancellationToken.None);
        await _unitOfWork.DidNotReceive().RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _fileTracker.DidNotReceive().RollbackCreatedFilesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenHandlerThrowsShouldRollbackAndRethrow()
    {
        var ct = TestContext.Current.CancellationToken;

        var behavior = new TransactionalBehavior<RegularCommand, Guid>(
            _unitOfWork,
            _fileTracker,
            Substitute.For<ILogger<TransactionalBehavior<RegularCommand, Guid>>>());

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await behavior.Handle(
                new RegularCommand(),
                (_, _) => ValueTask.FromException<Guid>(new InvalidOperationException("boom")),
                ct));

        await _unitOfWork.Received(1).RollbackTransactionAsync(CancellationToken.None);
        await _fileTracker.Received(1).RollbackCreatedFilesAsync(CancellationToken.None);
        await _unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AllApplicationCommandsShouldImplementITransactionalCommand()
    {
        var commandTypes = typeof(ITransactionalCommand).Assembly
            .GetTypes()
            .Where(t =>
                t is { IsClass: true, IsAbstract: false } &&
                t.Name.EndsWith("Command", StringComparison.Ordinal) &&
                typeof(IMessage).IsAssignableFrom(t))
            .ToList();

        commandTypes.ShouldNotBeEmpty();

        foreach (var commandType in commandTypes)
        {
            typeof(ITransactionalCommand).IsAssignableFrom(commandType)
                .ShouldBeTrue($"{commandType.FullName} must implement {nameof(ITransactionalCommand)}.");
        }
    }

    public sealed record RegularCommand : ICommand<Guid>, ITransactionalCommand;
}
