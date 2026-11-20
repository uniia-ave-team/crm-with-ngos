using System.Data.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Infrastructure.Extensions;
using Crm.Infrastructure.Helpers;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Crm.Infrastructure.Tests.Persistence.Interceptors;

/// <summary>
/// Verifies <see cref="RowLockCommandInterceptor"/> SQL rewriting, <see cref="RowLockQueryableExtensions.WithRowLock{T}"/>
/// LINQ translation, and DI registration on <see cref="ApplicationDbContext"/>.
/// </summary>
public class RowLockCommandInterceptorTests
{
    [Fact]
    public async Task InterceptorShouldAppendForUpdateOnlyToTaggedCommandsAndAvoidDuplication()
    {
        var ct = TestContext.Current.CancellationToken;
        var interceptor = new RowLockCommandInterceptor();
        var eventData = CreateCommandEventData();

        var taggedCommand = Substitute.For<DbCommand>();
        taggedCommand.CommandText = "-- __ROW_LOCK_FOR_UPDATE__\nSELECT p.\"Id\" FROM \"Project\" AS p WHERE p.\"Id\" = @p0 LIMIT 1";
        interceptor.ReaderExecuting(taggedCommand, eventData, default);
        taggedCommand.CommandText.ShouldEndWith("FOR UPDATE");

        // Re-running must not double-append FOR UPDATE
        await interceptor.ReaderExecutingAsync(taggedCommand, eventData, default, ct);
        taggedCommand.CommandText.IndexOf("FOR UPDATE", StringComparison.Ordinal)
            .ShouldBe(taggedCommand.CommandText.LastIndexOf("FOR UPDATE", StringComparison.Ordinal));

        // Trailing semicolon is preserved after FOR UPDATE
        var semicolonCommand = Substitute.For<DbCommand>();
        semicolonCommand.CommandText = "-- __ROW_LOCK_FOR_UPDATE__\nSELECT p.\"Id\" FROM \"Project\" AS p ORDER BY p.\"Id\";";
        await interceptor.ReaderExecutingAsync(semicolonCommand, eventData, default, ct);
        semicolonCommand.CommandText.ShouldEndWith("FOR UPDATE;");

        // Untagged queries remain untouched
        var untaggedCommand = Substitute.For<DbCommand>();
        const string originalSql = "SELECT p.\"Id\" FROM \"Project\" AS p";
        untaggedCommand.CommandText = originalSql;
        await interceptor.ReaderExecutingAsync(untaggedCommand, eventData, default, ct);
        untaggedCommand.CommandText.ShouldBe(originalSql);
    }

    [Fact]
    public void WithRowLockLinqQueryShouldTranslateToSingleTableExistsQueryWithTag()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=crm_rowlock_test;Username=postgres;Password=postgres")
            .Options;

        using var context = new ApplicationDbContext(options);
        var userId = Guid.NewGuid();

        string sql = context.Set<Project>()
            .AsNoTracking()
            .Where(p => p.Members.Any(m => m.UserId == userId && m.Claim >= ProjectClaim.Manage))
            .OrderBy(p => p.Id)
            .WithRowLock()
            .Select(p => p.Id)
            .ToQueryString();

        sql.ShouldContain("-- __ROW_LOCK_FOR_UPDATE__");
        sql.ShouldContain("EXISTS");
        sql.ShouldContain("ORDER BY");
        sql.ShouldNotContain("LEFT JOIN", Case.Insensitive);
    }

    [Fact]
    public void AddPersistenceShouldRegisterRowLockCommandInterceptorOnApplicationDbContext()
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

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var coreOptions = dbContext.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>();

        coreOptions.ShouldNotBeNull();
        coreOptions.Interceptors.ShouldNotBeNull();
        coreOptions.Interceptors.ShouldContain(i => i is RowLockCommandInterceptor);
    }

    private static CommandEventData CreateCommandEventData()
    {
        var eventDef = new EventDefinition(
            Substitute.For<ILoggingOptions>(),
            RelationalEventId.CommandExecuting,
            LogLevel.Debug,
            "CommandExecuting",
            _ => (_, _) => { });

        return new CommandEventData(
            eventDef,
            (_, _) => string.Empty,
            Substitute.For<DbConnection>(),
            Substitute.For<DbCommand>(),
            string.Empty,
            null,
            DbCommandMethod.ExecuteReader,
            Guid.NewGuid(),
            Guid.NewGuid(),
            true,
            false,
            DateTimeOffset.UtcNow,
            CommandSource.LinqQuery);
    }
}
