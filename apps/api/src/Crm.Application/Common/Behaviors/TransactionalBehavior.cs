using Crm.Application.Common.Consts;
using Crm.Application.Common.Interfaces;
using Crm.Application.Extensions;
using Crm.Application.Interfaces;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior that automatically wraps transactional commands in a database transaction
/// and coordinates post-commit or rollback physical file operations.
/// </summary>
/// <typeparam name="TRequest">The type of the transactional command being handled.</typeparam>
/// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
/// <param name="unitOfWork">The unit of work managing the database transaction boundary.</param>
/// <param name="fileTracker">The tracker coordinating deferred physical file deletions and rollbacks.</param>
/// <param name="logger">The logger instance for recording transaction lifecycle events.</param>
public sealed partial class TransactionalBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork,
    IFileTransactionTracker fileTracker,
    ILogger<TransactionalBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ITransactionalCommand, IMessage
{
    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TRequest message,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        string commandName = typeof(TRequest).Name;
        LogBeginningTransaction(logger, commandName);

        await unitOfWork.BeginTransactionAsync(cancellationToken);

        TResponse response;
        try
        {
            response = await next(message, cancellationToken);

            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            if (ex.IsClientError())
            {
                LogTransactionRolledBackOnClientError(logger, commandName, ex.GetType().Name, ex.Message);
            }
            else
            {
                LogTransactionRolledBack(logger, ex, commandName);
            }

            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);

            await fileTracker.RollbackCreatedFilesAsync(CancellationToken.None);

            throw;
        }

        await fileTracker.CommitPendingDeletionsAsync(CancellationToken.None);

        LogTransactionCommitted(logger, commandName);

        return response;
    }

    [LoggerMessage(EventId = LogEventIds.BeginningTransaction, Level = LogLevel.Information, Message = "Beginning database transaction for command: {CommandName}")]
    private static partial void LogBeginningTransaction(ILogger logger, string commandName);

    [LoggerMessage(EventId = LogEventIds.TransactionCommitted, Level = LogLevel.Information, Message = "Database transaction successfully committed for command: {CommandName}")]
    private static partial void LogTransactionCommitted(ILogger logger, string commandName);

    [LoggerMessage(EventId = LogEventIds.TransactionRolledBackOnClientError, Level = LogLevel.Warning, Message = "Transaction rolled back for command {CommandName} due to client error ({ExceptionType}): {Message}")]
    private static partial void LogTransactionRolledBackOnClientError(ILogger logger, string commandName, string exceptionType, string message);

    [LoggerMessage(EventId = LogEventIds.TransactionRolledBack, Level = LogLevel.Error, Message = "An error occurred while executing command {CommandName}. Transaction rolled back.")]
    private static partial void LogTransactionRolledBack(ILogger logger, Exception exception, string commandName);
}
