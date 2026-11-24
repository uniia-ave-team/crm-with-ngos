using System.Data.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Infrastructure.DependencyInjection;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSubstitute;
using Shouldly;

namespace Crm.Infrastructure.Tests.Persistence.Interceptors;

/// <summary>
/// Verifies translation of PostgreSQL and EF Core persistence exceptions into domain conflict exceptions
/// and DI registration of <see cref="DbExceptionTranslationInterceptor"/>.
/// </summary>
public class DbExceptionTranslationTests
{
    private readonly PostgresDbExceptionTranslator _translator = new();

    [Fact]
    public void TranslateShouldMapUniqueViolationToUniqueConflictWithoutLeakingSqlDetails()
    {
        var pgEx = new PostgresException("duplicate key value violates unique constraint \"IX_Tags_Title\"", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);
        var dbUpdateEx = new DbUpdateException("An error occurred while saving the entity changes.", pgEx);

        var result = _translator.Translate(dbUpdateEx);

        var conflict = result.ShouldBeOfType<ConflictException>();
        conflict.Kind.ShouldBe(ConflictKind.UniqueViolation);
        conflict.Message.ShouldBe(PostgresDbExceptionTranslator.UniqueViolationMessage);
        conflict.Message.ShouldNotContain("IX_Tags_Title");
        conflict.InnerException.ShouldBeSameAs(dbUpdateEx);
    }

    [Fact]
    public void TranslateShouldMapForeignKeyViolationToReferenceConflictWithoutLeakingSqlDetails()
    {
        var pgEx = new PostgresException("insert or update on table \"Task\" violates foreign key constraint \"FK_Task_Topic\"", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation);
        var dbUpdateEx = new DbUpdateException("An error occurred while saving the entity changes.", pgEx);

        var result = _translator.Translate(dbUpdateEx);

        var conflict = result.ShouldBeOfType<ConflictException>();
        conflict.Kind.ShouldBe(ConflictKind.ReferenceViolation);
        conflict.Message.ShouldBe(PostgresDbExceptionTranslator.ForeignKeyViolationMessage);
        conflict.Message.ShouldNotContain("FK_Task_Topic");
        conflict.InnerException.ShouldBeSameAs(dbUpdateEx);
    }

    [Fact]
    public void TranslateShouldMapDbUpdateConcurrencyExceptionToConcurrencyConflictException()
    {
        var concurrencyEx = new DbUpdateConcurrencyException("Database operation expected to affect 1 row(s) but actually affected 0 row(s).");

        var result = _translator.Translate(concurrencyEx);

        var conflict = result.ShouldBeOfType<ConcurrencyConflictException>();
        conflict.Kind.ShouldBe(ConflictKind.Concurrency);
        conflict.Message.ShouldBe(PostgresDbExceptionTranslator.ConcurrencyConflictMessage);
        conflict.InnerException.ShouldBeSameAs(concurrencyEx);
    }

    [Fact]
    public void TranslateShouldNotHijackIdentityConcurrencyExceptionOnAuthUserOrAuthRole()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=crm_translator_test;Username=postgres;Password=postgres")
            .Options;

        using var context = new ApplicationDbContext(options);

        var authUser = new AuthUser { Id = Guid.NewGuid(), Email = "user@ngo.org" };
        var authUserUpdateEntry = Substitute.For<IUpdateEntry>();
        authUserUpdateEntry.ToEntityEntry().Returns(context.Entry(authUser));
        var identityConcurrencyEx = new DbUpdateConcurrencyException("Identity concurrency conflict.", [authUserUpdateEntry]);

        _translator.Translate(identityConcurrencyEx).ShouldBeNull();

        var authRole = new AuthRole { Id = Guid.NewGuid(), Name = "Member" };
        var authRoleUpdateEntry = Substitute.For<IUpdateEntry>();
        authRoleUpdateEntry.ToEntityEntry().Returns(context.Entry(authRole));
        var roleConcurrencyEx = new DbUpdateConcurrencyException("Role concurrency conflict.", [authRoleUpdateEntry]);

        _translator.Translate(roleConcurrencyEx).ShouldBeNull();

        var task = new ProjectTask { Id = Guid.NewGuid(), Title = "Concurrent task" };
        var taskUpdateEntry = Substitute.For<IUpdateEntry>();
        taskUpdateEntry.ToEntityEntry().Returns(context.Entry(task));
        var taskConcurrencyEx = new DbUpdateConcurrencyException("Task concurrency conflict.", [taskUpdateEntry]);

        _translator.Translate(taskConcurrencyEx).ShouldBeOfType<ConcurrencyConflictException>();
    }

    [Fact]
    public void TranslateShouldUnwrapRetryLimitExceededExceptionWrappingDbUpdateException()
    {
        var pgEx = new PostgresException("duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);
        var dbUpdateEx = new DbUpdateException("save failed", pgEx);
        var retryEx = new RetryLimitExceededException("Max retries exceeded.", dbUpdateEx);

        var result = _translator.Translate(retryEx);

        var conflict = result.ShouldBeOfType<ConflictException>();
        conflict.Kind.ShouldBe(ConflictKind.UniqueViolation);
        conflict.InnerException.ShouldBeSameAs(retryEx);
    }

    [Fact]
    public void TranslateShouldReturnNullForNonConflictPostgresException()
    {
        var pgEx = new PostgresException("syntax error at or near \"FROM\"", "ERROR", "ERROR", PostgresErrorCodes.SyntaxError);
        var dbUpdateEx = new DbUpdateException("query failed", pgEx);

        var result = _translator.Translate(dbUpdateEx);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task InterceptorShouldTranslateSaveChangesAndNonSaveChangesCommandFailures()
    {
        var ct = TestContext.Current.CancellationToken;
        var interceptor = new DbExceptionTranslationInterceptor(_translator);
        var eventDefinition = new EventDefinition(
            Substitute.For<ILoggingOptions>(),
            CoreEventId.SaveChangesFailed,
            LogLevel.Error,
            "SaveChangesFailed",
            _ => (_, _) => { });

        var uniqueDbUpdateEx = new DbUpdateException(
            "save failed",
            new PostgresException("dup", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation));
        var saveEventData = new DbContextErrorEventData(eventDefinition, (_, _) => string.Empty, null!, uniqueDbUpdateEx);

        var syncSaveEx = Should.Throw<ConflictException>(() => interceptor.SaveChangesFailed(saveEventData));
        syncSaveEx.Kind.ShouldBe(ConflictKind.UniqueViolation);

        var asyncSaveEx = await Should.ThrowAsync<ConflictException>(() => interceptor.SaveChangesFailedAsync(saveEventData, ct));
        asyncSaveEx.Kind.ShouldBe(ConflictKind.UniqueViolation);

        var command = Substitute.For<DbCommand>();
        var fkEx = new PostgresException("fk", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation);
        var commandEventDef = new EventDefinition(
            Substitute.For<ILoggingOptions>(),
            RelationalEventId.CommandError,
            LogLevel.Error,
            "CommandError",
            _ => (_, _) => { });

        var nonSaveCommandEvent = new CommandErrorEventData(
            commandEventDef,
            (_, _) => string.Empty,
            Substitute.For<DbConnection>(),
            command,
            string.Empty,
            null,
            DbCommandMethod.ExecuteNonQuery,
            Guid.NewGuid(),
            Guid.NewGuid(),
            fkEx,
            true,
            false,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero,
            CommandSource.ExecuteDelete);

        var nonSaveSyncEx = Should.Throw<ConflictException>(() => interceptor.CommandFailed(command, nonSaveCommandEvent));
        nonSaveSyncEx.Kind.ShouldBe(ConflictKind.ReferenceViolation);

        var nonSaveAsyncEx = await Should.ThrowAsync<ConflictException>(() => interceptor.CommandFailedAsync(command, nonSaveCommandEvent, ct));
        nonSaveAsyncEx.Kind.ShouldBe(ConflictKind.ReferenceViolation);

        // SaveChanges commands should NOT be translated at CommandFailed level so retry strategies / SaveChangesFailed handle them
        var saveCommandEvent = new CommandErrorEventData(
            commandEventDef,
            (_, _) => string.Empty,
            Substitute.For<DbConnection>(),
            command,
            string.Empty,
            null,
            DbCommandMethod.ExecuteReader,
            Guid.NewGuid(),
            Guid.NewGuid(),
            fkEx,
            true,
            false,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero,
            CommandSource.SaveChanges);

        Should.NotThrow(() => interceptor.CommandFailed(command, saveCommandEvent));
        await Should.NotThrowAsync(() => interceptor.CommandFailedAsync(command, saveCommandEvent, ct));
    }

    [Fact]
    public void AddPersistenceShouldRegisterDbExceptionTranslationInterceptorOnApplicationDbContext()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSqlConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddPersistence(config);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        provider.GetRequiredService<IDbExceptionTranslator>().ShouldBeOfType<PostgresDbExceptionTranslator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var coreOptions = dbContext.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>();

        coreOptions.ShouldNotBeNull();
        coreOptions.Interceptors.ShouldNotBeNull();
        coreOptions.Interceptors.ShouldContain(i => i is DbExceptionTranslationInterceptor);
    }
}
