using Crm.Application.Interfaces;
using Crm.Cli.Commands;
using Crm.Cli.Parsing;
using Crm.Domain.Constants.Validation;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Crm.Cli.Tests.Commands;

/// <summary>
/// Unit tests for <see cref="NoticeCommand"/>.
/// </summary>
public class NoticeCommandTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 20, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SetSubcommandShouldPersistUtcTimestampsAndReturnZero()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = Substitute.For<IMaintenanceNoticeRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var command = new NoticeCommand(
            repository,
            unitOfWork,
            new FixedTimeProvider(FixedNow),
            NullLogger<NoticeCommand>.Instance);

        var args = CommandLineParser.Parse([
            "notice",
            "set",
            "--message",
            "  Planned database maintenance  ",
            "--starts-at",
            "2026-11-20T22:00:00Z",
            "--ends-at",
            "2026-11-21T01:00:00+02:00",
        ]);

        int exitCode = await command.ExecuteAsync(args, ct);

        exitCode.ShouldBe(0);
        await repository.Received(1).UpsertAsync(
            "Planned database maintenance",
            new DateTime(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 20, 23, 0, 0, DateTimeKind.Utc),
            FixedNow.UtcDateTime,
            ct);
        await unitOfWork.Received(1).SaveChangesAsync(ct);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClearSubcommandShouldBeIdempotentAndReturnZero(bool existed)
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = Substitute.For<IMaintenanceNoticeRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository.ClearAsync(ct).Returns(existed);

        var command = new NoticeCommand(
            repository,
            unitOfWork,
            new FixedTimeProvider(FixedNow),
            NullLogger<NoticeCommand>.Instance);

        var args = CommandLineParser.Parse(["notice", "clear"]);

        int exitCode = await command.ExecuteAsync(args, ct);

        exitCode.ShouldBe(0);
        await repository.Received(1).ClearAsync(ct);
    }

    [Theory]
    [InlineData("notice")]
    [InlineData("notice", "unknown")]
    [InlineData("notice", "set", "--starts-at", "2026-11-20T22:00:00Z", "--ends-at", "2026-11-20T23:00:00Z")]
    [InlineData("notice", "set", "--message", "Maintenance", "--starts-at", "invalid", "--ends-at", "2026-11-20T23:00:00Z")]
    [InlineData("notice", "set", "--message", "Maintenance", "--starts-at", "2026-11-20T23:00:00Z", "--ends-at", "2026-11-20T22:00:00Z")]
    [InlineData("notice", "set", "--message", "Maintenance", "--starts-at", "2026-11-20T22:00:00Z", "--ends-at", "2026-11-20T22:00:00Z")]
    public async Task InvalidArgumentsShouldReturnNonZeroWithoutWritingToDatabase(params string[] rawArgs)
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = Substitute.For<IMaintenanceNoticeRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var command = new NoticeCommand(
            repository,
            unitOfWork,
            new FixedTimeProvider(FixedNow),
            NullLogger<NoticeCommand>.Instance);

        var args = CommandLineParser.Parse(rawArgs);

        int exitCode = await command.ExecuteAsync(args, ct);

        exitCode.ShouldBe(1);
        await repository.DidNotReceive().UpsertAsync(
            Arg.Any<string>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetSubcommandShouldRejectMessageExceedingMaxLength()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = Substitute.For<IMaintenanceNoticeRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var command = new NoticeCommand(
            repository,
            unitOfWork,
            new FixedTimeProvider(FixedNow),
            NullLogger<NoticeCommand>.Instance);

        var args = CommandLineParser.Parse([
            "notice",
            "set",
            "--message",
            new string('a', MaintenanceNoticeValidationConstants.MaxMessageLength + 1),
            "--starts-at",
            "2026-11-20T22:00:00Z",
            "--ends-at",
            "2026-11-20T23:00:00Z",
        ]);

        int exitCode = await command.ExecuteAsync(args, ct);

        exitCode.ShouldBe(1);
        await repository.DidNotReceive().UpsertAsync(
            Arg.Any<string>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
