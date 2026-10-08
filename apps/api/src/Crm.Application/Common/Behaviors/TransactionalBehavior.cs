using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior that automatically wraps transactional commands in a database transaction.
/// </summary>
public partial class TransactionalBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork,
    IFileTransactionTracker fileTracker,
    ILogger<TransactionalBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ITransactionalCommand
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        LogBeginningTransaction(logger, typeof(TRequest).Name);

        await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next(cancellationToken);

            await unitOfWork.CommitTransactionAsync(cancellationToken);

            await fileTracker.CommitPendingDeletionsAsync(cancellationToken);

            LogTransactionCommitted(logger, typeof(TRequest).Name);

            return response;
        }
        catch (Exception ex)
        {
            LogTransactionRolledBack(logger, ex, typeof(TRequest).Name);

            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);

            await fileTracker.RollbackCreatedFilesAsync(CancellationToken.None);

            throw;
        }
    }

    [LoggerMessage(EventId = LogEventIds.BeginningTransaction, Level = LogLevel.Information, Message = "Beginning database transaction for command: {CommandName}")]
    private static partial void LogBeginningTransaction(ILogger logger, string commandName);

    [LoggerMessage(EventId = LogEventIds.TransactionCommitted, Level = LogLevel.Information, Message = "Database transaction successfully committed for command: {CommandName}")]
    private static partial void LogTransactionCommitted(ILogger logger, string commandName);

    [LoggerMessage(EventId = LogEventIds.TransactionRolledBack, Level = LogLevel.Error, Message = "An error occurred while executing command {CommandName}. Transaction rolled back.")]
    private static partial void LogTransactionRolledBack(ILogger logger, Exception exception, string commandName);
}
