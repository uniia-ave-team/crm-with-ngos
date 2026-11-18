using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Crm.Api.DemoMode;
using Crm.Application.Dtos.Common;
using Crm.Application.Dtos.Role.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Services;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Crm.Api.Tests.DemoMode;

/// <summary>
/// Unit tests for the isolated Demo Mode decorators and fail-safe guards:
/// <see cref="DemoMediator"/>, <see cref="DemoUnitOfWork"/>, <see cref="DemoFileTransactionTracker"/>,
/// <see cref="DemoFileStorageService"/>, <see cref="DemoRolePermissionsCache"/>,
/// <see cref="DemoRefreshTokenStore"/>, <see cref="DemoUserRefreshTokenRepository"/>,
/// and <see cref="DemoDbWriteGuardInterceptor"/>.
/// </summary>
public class DemoModeDecoratorsTests
{
    [Fact]
    public async Task DemoMediatorAndUnitOfWorkWhenDemoModeActiveShouldRollbackRegularCommandsAndCommitAllowedCommands()
    {
        var ct = TestContext.Current.CancellationToken;
        var demoState = Substitute.For<IDemoModeState>();
        demoState.IsActive.Returns(true);
        var executionContext = new DemoExecutionContext();

        var innerUow = Substitute.For<IUnitOfWork>();
        var uow = new DemoUnitOfWork(innerUow, executionContext, Substitute.For<ILogger<DemoUnitOfWork>>());

        var innerTracker = Substitute.For<IFileTransactionTracker>();
        var tracker = new DemoFileTransactionTracker(innerTracker, executionContext);

        var expectedId = Guid.NewGuid();
        var innerMediator = new StubMediator(async (_, token) =>
        {
            await uow.BeginTransactionAsync(token);
            tracker.RegisterFileForDeletion("old.png", "users");
            await uow.CommitTransactionAsync(token);
            await tracker.CommitPendingDeletionsAsync(token);
            return expectedId;
        });

        var mediator = new DemoMediator(innerMediator, demoState, executionContext);

        // Regular ITransactionalCommand -> dry-run rollback; RegisterFileForDeletion suppressed
        var regularResult = await mediator.Send(new RegularCommand(), ct);
        regularResult.ShouldBe(expectedId);
        await innerUow.Received(1).RollbackTransactionAsync(CancellationToken.None);
        await innerUow.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
        innerTracker.DidNotReceive().RegisterFileForDeletion(Arg.Any<string?>(), Arg.Any<string>());
        await innerTracker.Received(1).RollbackCreatedFilesAsync(CancellationToken.None);
        await innerTracker.DidNotReceive().CommitPendingDeletionsAsync(Arg.Any<CancellationToken>());

        // IDemoAllowed command -> real commit
        var allowedMediator = new DemoMediator(
            new StubMediator(async (_, token) =>
            {
                await uow.BeginTransactionAsync(token);
                await uow.CommitTransactionAsync(token);
                await tracker.CommitPendingDeletionsAsync(token);
                return "committed";
            }),
            demoState,
            executionContext);

        var allowedResult = await allowedMediator.Send(new DemoAllowedCommand(), ct);
        allowedResult.ShouldBe("committed");
        await innerUow.Received(1).CommitTransactionAsync(ct);
        await innerTracker.Received(1).CommitPendingDeletionsAsync(ct);

        // [DemoAllow] attributed command -> real commit
        var attrResult = await allowedMediator.Send(new DemoAllowAttributedCommand(), ct);
        attrResult.ShouldBe("committed");
        await innerUow.Received(2).CommitTransactionAsync(ct);
        await innerTracker.Received(2).CommitPendingDeletionsAsync(ct);
    }

    [Fact]
    public async Task DemoMediatorPublishWhenDemoModeActiveShouldEnterDryRunScope()
    {
        var ct = TestContext.Current.CancellationToken;
        var demoState = Substitute.For<IDemoModeState>();
        demoState.IsActive.Returns(true);
        var executionContext = new DemoExecutionContext();

        DemoExecutionMode? observedMode = null;
        var innerMediator = new StubMediator(
            (_, _) => Task.FromResult<object?>(null),
            onPublish: () => observedMode = executionContext.Mode);

        var mediator = new DemoMediator(innerMediator, demoState, executionContext);
        await mediator.Publish(new SampleNotification(), ct);

        observedMode.ShouldBe(DemoExecutionMode.DryRun);
        executionContext.Mode.ShouldBe(DemoExecutionMode.None);
    }

    [Fact]
    public async Task DemoMediatorWhenAddOrRemoveRoleClaimHitsNotFoundInDemoModeShouldReturnUnitValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var demoState = Substitute.For<IDemoModeState>();
        demoState.IsActive.Returns(true);
        var executionContext = new DemoExecutionContext();

        var innerMediator = new StubMediator((_, _) =>
            Task.FromException<object?>(new EntityNotFoundException<object>(Guid.NewGuid())));

        var mediator = new DemoMediator(innerMediator, demoState, executionContext);

        var addResult = await mediator.Send(
            new AddClaimToRoleCommand(Guid.NewGuid(), AccessRight.ViewUser.ToClaimValue()),
            ct);
        addResult.ShouldBe(Unit.Value);

        var removeResult = await mediator.Send(
            new RemoveClaimFromRoleCommand(Guid.NewGuid(), AccessRight.ViewUser.ToClaimValue()),
            ct);
        removeResult.ShouldBe(Unit.Value);
    }

    [Fact]
    public async Task DemoRefreshTokenStoreAndRepositoryShouldStoreTokensInMemoryWithCapacityBoundAndNeverTouchInnerDb()
    {
        var ct = TestContext.Current.CancellationToken;
        var demoState = Substitute.For<IDemoModeState>();
        demoState.IsActive.Returns(true);
        var executionContext = new DemoExecutionContext();

        var store = new DemoRefreshTokenStore(
            MsOptions.Create(new DemoModeOptions { Enabled = true, MaxInMemoryRefreshTokens = 2 }),
            TimeProvider.System);
        var innerRepo = Substitute.For<IUserRefreshTokenRepository>();
        var repo = new DemoUserRefreshTokenRepository(innerRepo, store, demoState, executionContext);

        var userId = Guid.NewGuid();
        var future = DateTime.UtcNow.AddHours(1);

        // SessionAuth persists in memory only
        using (executionContext.EnterScope(DemoExecutionMode.SessionAuth))
        {
            await repo.CreateAsync(userId, "token-1", future, ct);
            await repo.CreateAsync(userId, "token-2", future, ct);
            (await repo.IsValidTokenAsync(userId, "token-1", ct)).ShouldBeTrue();
            (await repo.IsValidTokenAsync(userId, "token-2", ct)).ShouldBeTrue();

            // Capacity bound evicts oldest token
            await repo.CreateAsync(userId, "token-3", future, ct);
            (await repo.IsValidTokenAsync(userId, "token-1", ct)).ShouldBeFalse();
            (await repo.IsValidTokenAsync(userId, "token-3", ct)).ShouldBeTrue();

            // Logout removes only the specific token
            await repo.RemoveTokenAsync("token-2", ct);
            (await repo.IsValidTokenAsync(userId, "token-2", ct)).ShouldBeFalse();
            (await repo.IsValidTokenAsync(userId, "token-3", ct)).ShouldBeTrue();
        }

        // DryRun mutations do not pollute store or revoke active sessions
        using (executionContext.EnterScope(DemoExecutionMode.DryRun))
        {
            await repo.CreateAsync(userId, "phantom-token", future, ct);
            (await repo.IsValidTokenAsync(userId, "phantom-token", ct)).ShouldBeFalse();

            await repo.RevokeAllUserTokensAsync(userId, ct);
            (await repo.IsValidTokenAsync(userId, "token-3", ct)).ShouldBeTrue();
        }

        await repo.RemoveExpiredTokensAsync(ct);

        await innerRepo.DidNotReceive().CreateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await innerRepo.DidNotReceive().RemoveTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await innerRepo.DidNotReceive().RevokeAllUserTokensAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await innerRepo.DidNotReceive().RemoveExpiredTokensAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DemoDbWriteGuardInterceptorShouldBlockUnguardedMutatingSqlDuringHttpRequestAndAllowReadsAndTransactions()
    {
        var demoState = Substitute.For<IDemoModeState>();
        demoState.IsActive.Returns(true);
        var executionContext = new DemoExecutionContext();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(new DefaultHttpContext());

        var interceptor = new DemoDbWriteGuardInterceptor(
            demoState,
            executionContext,
            httpContextAccessor,
            Substitute.For<ILogger<DemoDbWriteGuardInterceptor>>());

        DemoDbWriteGuardInterceptor.IsMutatingSql("SELECT * FROM \"Users\"").ShouldBeFalse();
        DemoDbWriteGuardInterceptor.IsMutatingSql("INSERT INTO \"Users\" VALUES (1)").ShouldBeTrue();
        DemoDbWriteGuardInterceptor.IsMutatingSql("UPDATE \"Users\" SET \"IsActive\" = false").ShouldBeTrue();
        DemoDbWriteGuardInterceptor.IsMutatingSql("DELETE FROM \"UserRefreshTokens\"").ShouldBeTrue();

        var unguardedInsert = new StubDbCommand("INSERT INTO \"Users\" VALUES (1)", transaction: null);
        Should.Throw<InvalidOperationException>(() =>
            interceptor.NonQueryExecuting(unguardedInsert, null!, default));

        var selectCmd = new StubDbCommand("SELECT * FROM \"Users\"", transaction: null);
        Should.NotThrow(() => interceptor.ReaderExecuting(selectCmd, null!, default));

        // Inside DryRun WITH an active DB transaction -> allowed (DemoUnitOfWork will roll back)
        using (executionContext.EnterScope(DemoExecutionMode.DryRun))
        {
            var txInsert = new StubDbCommand("INSERT INTO \"Users\" VALUES (1)", transaction: new StubDbTransaction());
            Should.NotThrow(() => interceptor.NonQueryExecuting(txInsert, null!, default));

            // Inside DryRun WITHOUT a DB transaction -> still blocked
            Should.Throw<InvalidOperationException>(() =>
                interceptor.NonQueryExecuting(unguardedInsert, null!, default));
        }

        // Outside HTTP request (startup migration / seeder) -> allowed
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        Should.NotThrow(() => interceptor.NonQueryExecuting(unguardedInsert, null!, default));
    }

    [Fact]
    public async Task DemoUnitOfWorkSaveChangesAsyncShouldThrowWhenCalledOutsideDryRunDuringHttpRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var demoState = Substitute.For<IDemoModeState>();
        demoState.IsActive.Returns(true);
        var executionContext = new DemoExecutionContext();
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(new DefaultHttpContext());

        var innerUow = Substitute.For<IUnitOfWork>();
        var uow = new DemoUnitOfWork(
            innerUow,
            executionContext,
            Substitute.For<ILogger<DemoUnitOfWork>>(),
            demoState,
            httpContextAccessor);

        await Should.ThrowAsync<InvalidOperationException>(() => uow.SaveChangesAsync(ct));

        using (executionContext.EnterScope(DemoExecutionMode.DryRun))
        {
            await Should.NotThrowAsync(() => uow.SaveChangesAsync(ct));
        }
    }

    [Fact]
    public async Task DemoRolePermissionsCacheWhenDemoModeActiveShouldIgnoreMutationsAndEvictionsButAllowSeeding()
    {
        var ct = TestContext.Current.CancellationToken;
        var roleId = Guid.NewGuid();
        var viewUserClaim = AccessRight.ViewUser.ToClaimValue();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var roleRepo = Substitute.For<IAuthRoleRepository>();
        roleRepo.GetAllRolesPermissionsAsync(ct).Returns(new Dictionary<Guid, HashSet<string>>
        {
            [roleId] = [viewUserClaim],
        });

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IAuthRoleRepository)).Returns(roleRepo);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var demoModeState = Substitute.For<IDemoModeState>();
        demoModeState.IsActive.Returns(true);

        using var innerCache = new RolePermissionsCache(
            memoryCache,
            scopeFactory,
            MsOptions.Create(new JwtOptions { AccessTokenExpiryMinutes = 15 }),
            Substitute.For<ILogger<RolePermissionsCache>>());

        var cache = new DemoRolePermissionsCache(innerCache, demoModeState);

        await cache.SeedAllRolesCacheAsync(ct);
        (await cache.GetRolePermissionsAsync(roleId, ct)).ShouldContain(viewUserClaim);

        await cache.SetRolePermissionsAsync(roleId, [], ct);
        (await cache.GetRolePermissionsAsync(roleId, ct)).ShouldContain(viewUserClaim);

        await cache.RemoveRolePermissionsAsync(roleId, ct);
        (await cache.GetRolePermissionsAsync(roleId, ct)).ShouldContain(viewUserClaim);
    }

    [Fact]
    public async Task DemoFileStorageServiceWhenDemoModeActiveShouldValidateWithoutWritingOrDeletingPhysicalFiles()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = Path.Combine(Path.GetTempPath(), $"crm-demo-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var usersDir = Path.Combine(tempDir, "users");
            Directory.CreateDirectory(usersDir);
            var existingFile = Path.Combine(usersDir, "seeded-avatar.png");
            await File.WriteAllBytesAsync(existingFile, [1, 2, 3], ct);

            var storageOptions = MsOptions.Create(new FileStorageOptions { UploadDirectory = tempDir, BaseUrl = "http://localhost" });
            var imageValidator = Substitute.For<IImageValidator>();
            imageValidator.Validate(Arg.Any<Stream>(), "avatar.png")
                .Returns(new FileValidationResult(IsValid: true));
            imageValidator.Validate(Arg.Any<Stream>(), "bad.png")
                .Returns(new FileValidationResult(IsValid: false));

            var demoModeState = Substitute.For<IDemoModeState>();
            demoModeState.IsActive.Returns(true);

            var innerStorage = new LocalFileStorageService(storageOptions, imageValidator);
            var storage = new DemoFileStorageService(innerStorage, storageOptions, imageValidator, demoModeState);

            using var nonSeekable = new NonSeekableStream();
            await Should.ThrowAsync<ArgumentException>(() => storage.SaveFileAsync(nonSeekable, "report.pdf", "attachments", ct));

            using var docStream = new MemoryStream([10, 20, 30, 40]);
            var savedFile = await storage.SaveFileAsync(docStream, "report.pdf", "attachments", ct);
            savedFile.ShouldEndWith(".pdf");
            Directory.Exists(Path.Combine(tempDir, "attachments")).ShouldBeFalse();

            using var validImgStream = new MemoryStream([1, 2, 3, 4]);
            var savedImage = await storage.SaveImageAsync(validImgStream, "avatar.png", "users", ct);
            savedImage.ShouldEndWith(".png");

            using var badImgStream = new MemoryStream([1, 2, 3, 4]);
            await Should.ThrowAsync<ArgumentException>(() => storage.SaveImageAsync(badImgStream, "bad.png", "users", ct));

            await storage.DeleteFileAsync("seeded-avatar.png", "users", ct);
            File.Exists(existingFile).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    public sealed record RegularCommand : IRequest<Guid>, ITransactionalCommand;

    public sealed record DemoAllowedCommand : IRequest<string>, ITransactionalCommand, IDemoAllowed;

    [DemoAllow]
    public sealed record DemoAllowAttributedCommand : IRequest<string>, ITransactionalCommand;

    public sealed record SampleNotification : INotification;

    private sealed class NonSeekableStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class StubDbTransaction : DbTransaction
    {
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        protected override DbConnection? DbConnection => null;

        public override void Commit()
        {
        }

        public override void Rollback()
        {
        }
    }

    private sealed class StubDbCommand(string commandText, DbTransaction? transaction) : DbCommand
    {
        [AllowNull]
        public override string CommandText { get; set; } = commandText;

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; } = CommandType.Text;

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection? DbConnection { get; set; }

        protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();

        protected override DbTransaction? DbTransaction { get; set; } = transaction;

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => 0;

        public override object? ExecuteScalar() => null;

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter() => throw new NotSupportedException();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    }

    private sealed class StubMediator(
        Func<object, CancellationToken, Task<object?>> handler,
        Action? onPublish = null) : IMediator
    {
        public async ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => (TResponse)(await handler(request, cancellationToken))!;

        public async ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
            => (TResponse)(await handler(command, cancellationToken))!;

        public async ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
            => (TResponse)(await handler(query, cancellationToken))!;

        public async ValueTask<object?> Send(object message, CancellationToken cancellationToken = default)
            => await handler(message, cancellationToken);

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            onPublish?.Invoke();
            return ValueTask.CompletedTask;
        }

        public ValueTask Publish(object notification, CancellationToken cancellationToken = default)
        {
            onPublish?.Invoke();
            return ValueTask.CompletedTask;
        }
    }
}
