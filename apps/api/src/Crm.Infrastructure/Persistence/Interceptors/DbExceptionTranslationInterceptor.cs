using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Crm.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Intercepts EF Core save and command failures to translate provider-specific exceptions
/// into domain exceptions via <see cref="IDbExceptionTranslator"/>.
/// </summary>
/// <param name="translator">The exception translator for the active database provider.</param>
public sealed class DbExceptionTranslationInterceptor(IDbExceptionTranslator translator)
    : SaveChangesInterceptor, IDbCommandInterceptor
{
    /// <inheritdoc />
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        ThrowTranslatedIfApplicable(eventData.Exception);
        base.SaveChangesFailed(eventData);
    }

    /// <inheritdoc />
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ThrowTranslatedIfApplicable(eventData.Exception);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    /// <inheritdoc />
    public void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        if (eventData.CommandSource != CommandSource.SaveChanges)
        {
            ThrowTranslatedIfApplicable(eventData.Exception);
        }
    }

    /// <inheritdoc />
    public Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.CommandSource != CommandSource.SaveChanges)
        {
            ThrowTranslatedIfApplicable(eventData.Exception);
        }

        return Task.CompletedTask;
    }

    private void ThrowTranslatedIfApplicable(Exception exception)
    {
        var translated = translator.Translate(exception);
        if (translated is not null && !ReferenceEquals(translated, exception))
        {
            throw translated;
        }
    }
}
