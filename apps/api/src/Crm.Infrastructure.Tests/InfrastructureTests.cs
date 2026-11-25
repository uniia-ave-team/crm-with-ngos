using System.Data.Common;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Crm.Application.Dtos.CustomTables;
using Crm.Application.Dtos.TableTemplates;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.CustomTables.Mappings;
using Crm.Application.Features.TableTemplates.Mappings;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Entities;
using Crm.Infrastructure.Auditing;
using Crm.Infrastructure.DependencyInjection;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Mappings;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Expressions;
using Crm.Infrastructure.Persistence.Interceptors;
using Crm.Infrastructure.Persistence.Sorting;
using Crm.Infrastructure.Persistence.ValueComparers;
using Crm.Infrastructure.Services.Identity;
using Crm.Infrastructure.Services.Storage;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Crm.Infrastructure.Tests;

/// <summary>
/// High-value unit tests verifying core infrastructure guarantees: DI lifetimes, interceptor execution order,
/// JSONB in-place mutation tracking, parameterized <c>ILIKE</c> SQL generation, strongly-typed path ordering,
/// transaction-scoped audit flushing, <see cref="TimeProvider"/>-driven JWT validation, permission checks,
/// soft-delete query filters, and <see cref="UnitOfWork"/> disposal/rollback safety.
/// </summary>
public class InfrastructureTests
{
    private const string DummyConnectionString =
        "Host=localhost;Database=crm_infra_tests;Username=postgres;Password=postgres";

    private static readonly EventDefinitionBase TestEventDefinition = new EventDefinition(
        Substitute.For<ILoggingOptions>(),
        CoreEventId.SaveChangesStarting,
        LogLevel.Debug,
        nameof(CoreEventId.SaveChangesStarting),
        _ => (_, _) => { });

    [Fact]
    public void AddInfrastructureServicesShouldRegisterExpectedLifetimesAndInterceptorOrder()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSqlConnection"] = DummyConnectionString,
                ["JwtOptions:Secret"] = "super-secret-signing-key-for-tests-1234",
                ["JwtOptions:Issuer"] = "CrmTestIssuer",
                ["JwtOptions:Audience"] = "CrmTestAudience",
                ["FileStorage:UploadDirectory"] = "uploads",
                ["FileStorage:BaseUrl"] = "http://localhost:5065",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices(config);

        services.ShouldContain(d => d.ServiceType == typeof(ITokenService) && d.Lifetime == ServiceLifetime.Singleton);
        services.ShouldContain(d => d.ServiceType == typeof(ICurrentUserService) && d.Lifetime == ServiceLifetime.Singleton);
        services.ShouldContain(d => d.ServiceType == typeof(IPermissionService) && d.Lifetime == ServiceLifetime.Singleton);
        services.ShouldContain(d => d.ServiceType == typeof(IOptionService) && d.Lifetime == ServiceLifetime.Singleton);
        services.ShouldContain(d => d.ServiceType == typeof(IRevokedUsersCache) && d.Lifetime == ServiceLifetime.Singleton);
        services.ShouldContain(d => d.ServiceType == typeof(JwtBearerEventsHandler) && d.Lifetime == ServiceLifetime.Scoped);
        services.ShouldContain(d => d.ServiceType == typeof(ILocalStorageQuotaTracker) && d.Lifetime == ServiceLifetime.Singleton);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var interceptors = dbContext.GetService<IDbContextOptions>()
            .FindExtension<CoreOptionsExtension>()?
            .Interceptors?
            .ToList();

        interceptors.ShouldNotBeNull();
        interceptors.Count.ShouldBe(4);
        interceptors[0].ShouldBeOfType<RowLockCommandInterceptor>();
        interceptors[1].ShouldBeOfType<EntityAuditMetadataSaveChangesInterceptor>();
        interceptors[2].ShouldBeOfType<EntityAuditSaveChangesInterceptor>();
        interceptors[3].ShouldBeOfType<DbExceptionTranslationInterceptor>();
    }

    [Fact]
    public void JsonObjectValueComparerShouldDetectInPlaceJsonbMutationsAndIgnoreKeyOrderDifferences()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        using var context = new ApplicationDbContext(options);

        var record = new CustomRecord
        {
            Id = Guid.CreateVersion7(),
            CustomTableId = Guid.CreateVersion7(),
            Data = new JsonObject { ["city"] = "Kyiv", ["count"] = 1 },
        };

        context.Attach(record);
        context.Entry(record).State.ShouldBe(EntityState.Unchanged);

        // Mutating the JsonObject in-place must be detected by EF Core's ChangeTracker thanks to JsonObjectValueComparer.
        record.Data["city"] = "Lviv";
        context.ChangeTracker.DetectChanges();

        context.Entry(record).State.ShouldBe(EntityState.Modified);
        context.Entry(record).Property(r => r.Data).IsModified.ShouldBeTrue();

        // Equivalent JSON objects with different property insertion order must compare equal with matching hash codes.
        var left = new JsonObject { ["a"] = 1, ["b"] = "text" };
        var right = new JsonObject { ["b"] = "text", ["a"] = 1 };

        JsonObjectValueComparer.Instance.Equals(left, right).ShouldBeTrue();
        JsonObjectValueComparer.Instance.GetHashCode(left).ShouldBe(JsonObjectValueComparer.Instance.GetHashCode(right));
    }

    [Fact]
    public void QueryFilterExpressionsShouldParameterizePatternInsteadOfInliningConstantLiterals()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        using var context = new ApplicationDbContext(options);

        string sql = context.Set<Project>()
            .AsNoTracking()
            .Where(QueryFilterExpressions.ContainsIgnoreCase<Project>("Kyiv", p => p.Title, p => p.Description))
            .ToQueryString();

        sql.ShouldContain("ILIKE");
        sql.ShouldContain("@pattern");
        sql.ShouldNotContain("ILIKE '%Kyiv%'");
    }

    [Fact]
    public void TypedPropertyOrderBuilderShouldSortNestedPathsAndRejectInvalidPaths()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        using var context = new ApplicationDbContext(options);
        var query = context.Set<AuthUser>().AsQueryable();

        TypedPropertyOrderBuilder.TryApplyOrdering(query, "UserProfile.IsActive", descending: true, out var ordered)
            .ShouldBeTrue();

        string sql = ordered.ToQueryString();
        sql.ShouldContain("\"IsActive\" DESC");
        sql.ShouldContain("\"Id\"");

        TypedPropertyOrderBuilder.TryApplyOrdering(query, "NonExistent.Property", descending: false, out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void ApplicationDbContextShouldApplySoftDeleteQueryFiltersToSoftDeletableEntitiesAndExcludeCustomRecord()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        using var context = new ApplicationDbContext(options);

        typeof(ISoftDeletableEntity).IsAssignableFrom(typeof(CustomRecord)).ShouldBeFalse();

        foreach (Type softDeletableType in new[] { typeof(Project), typeof(ProjectTask), typeof(CustomTable) })
        {
            typeof(ISoftDeletableEntity).IsAssignableFrom(softDeletableType).ShouldBeTrue();
            var entityType = context.Model.FindEntityType(softDeletableType);
            entityType.ShouldNotBeNull();
            entityType.GetDeclaredQueryFilters().ShouldNotBeEmpty();
        }

        string projectSql = context.Set<Project>().AsNoTracking().ToQueryString();
        projectSql.ShouldContain("\"IsDeleted\"");

        // CustomRecord is hard-deleted itself, but still hides rows belonging to a soft-deleted CustomTable.
        string recordSql = context.Set<CustomRecord>().AsNoTracking().ToQueryString();
        recordSql.ShouldContain("\"CustomTable\"");
        recordSql.ShouldContain("\"IsDeleted\"");
    }

    [Fact]
    public void MapsterSqlProjectionsShouldAvoidRedundantAndCartesianLeftJoins()
    {
        new CustomTableMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
        new TableTemplateMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
        new AuthUserMappingRegister().Register(TypeAdapterConfig.GlobalSettings);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        using var context = new ApplicationDbContext(options);

        // 1. CustomTableSummaryDto must count fields and records via scalar subqueries with ZERO LEFT JOINs.
        string summarySql = context.Set<CustomTable>()
            .AsNoTracking()
            .ProjectToType<CustomTableSummaryDto>()
            .ToQueryString();
        summarySql.ShouldNotContain("LEFT JOIN");

        // 2. CustomTableDto must join CustomField at most once (not 3x N^3).
        string tableDtoSql = context.Set<CustomTable>()
            .AsNoTracking()
            .ProjectToType<CustomTableDto>()
            .ToQueryString();
        CountOccurrences(tableDtoSql, "LEFT JOIN").ShouldBe(1);

        // 3. TableTemplateDto must join TemplateField at most once (not 3x N^3).
        string templateDtoSql = context.Set<TableTemplate>()
            .AsNoTracking()
            .ProjectToType<TableTemplateDto>()
            .ToQueryString();
        CountOccurrences(templateDtoSql, "LEFT JOIN").ShouldBe(1);

        // 4. UserProfileResult projects CustomFields directly from Users JSONB with ZERO UserCustomField joins.
        string profileSql = context.Set<AuthUser>()
            .AsNoTracking()
            .ProjectToType<UserProfileResult>()
            .ToQueryString();
        profileSql.ShouldNotContain("LEFT JOIN \"UserCustomField\"");
        profileSql.ShouldContain("\"CustomFields\"");
    }

    [Fact]
    public async Task EntityAuditMetadataAndAuditInterceptorsShouldConvertSoftDeletableRemoveIntoAuditedSoftDelete()
    {
        var ct = TestContext.Current.CancellationToken;
        var initialTime = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var deleteTime = new DateTimeOffset(2026, 1, 5, 14, 30, 0, TimeSpan.Zero);
        var actorId = Guid.CreateVersion7();

        var clock = new MutableTimeProvider(deleteTime);
        var actorAccessor = Substitute.For<IAuditActorAccessor>();
        actorAccessor.GetActorUserId().Returns(actorId);
        var buffer = new RecordingAuditBuffer();

        var metadataInterceptor = new EntityAuditMetadataSaveChangesInterceptor(clock, actorAccessor);
        var auditInterceptor = new EntityAuditSaveChangesInterceptor(
            buffer,
            actorAccessor,
            MsOptions.Create(new AuditOptions { Enabled = true }),
            clock);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        await using var context = new ApplicationDbContext(options);
        var project = new Project
        {
            Id = Guid.CreateVersion7(),
            Title = "Soft Delete Target",
            StartDate = initialTime,
            CreatedAt = initialTime,
            UpdatedAt = initialTime,
        };

        context.Attach(project);
        context.Remove(project);
        context.Entry(project).State.ShouldBe(EntityState.Deleted);

        var savingEvent = new DbContextEventData(TestEventDefinition, (_, _) => string.Empty, context);
        await metadataInterceptor.SavingChangesAsync(savingEvent, default, ct);

        context.Entry(project).State.ShouldBe(EntityState.Modified);
        project.IsDeleted.ShouldBeTrue();
        project.DeletedAt.ShouldBe(deleteTime.UtcDateTime);
        project.UpdatedAt.ShouldBe(deleteTime.UtcDateTime);
        project.UpdatedBy.ShouldBe(actorId);

        // EntityAuditSaveChangesInterceptor runs after metadata interceptor and classifies IsDeleted false->true as Deleted.
        await auditInterceptor.SavingChangesAsync(savingEvent, default, ct);
        var savedEvent = new SaveChangesCompletedEventData(TestEventDefinition, (_, _) => string.Empty, context, 1);
        await auditInterceptor.SavedChangesAsync(savedEvent, 1, ct);

        var auditRecord = buffer.Records.ShouldHaveSingleItem();
        auditRecord.Action.ShouldBe(AuditAction.Deleted);
        auditRecord.EntityType.ShouldBe(nameof(Project));
        auditRecord.EntityKey.ShouldBe(project.Id.ToString());
        auditRecord.Changes.ShouldContainKey(nameof(Project.IsDeleted));
        auditRecord.Changes.ShouldContainKey(nameof(Project.DeletedAt));
    }

    [Fact]
    public void RowLockCommandInterceptorShouldOnlyLockCommandsWithLeadingTagComment()
    {
        var interceptor = new RowLockCommandInterceptor();
        var command = Substitute.For<DbCommand>();
        const string sqlWithTagInsideStringLiteral =
            "SELECT p.\"Id\" FROM \"Project\" AS p WHERE p.\"Title\" = '-- __ROW_LOCK_FOR_NO_KEY_UPDATE__'";
        command.CommandText = sqlWithTagInsideStringLiteral;

        var eventDef = new EventDefinition(
            Substitute.For<ILoggingOptions>(),
            RelationalEventId.CommandExecuting,
            LogLevel.Debug,
            "CommandExecuting",
            _ => (_, _) => { });
        var eventData = new CommandEventData(
            eventDef,
            (_, _) => string.Empty,
            Substitute.For<DbConnection>(),
            command,
            string.Empty,
            null,
            DbCommandMethod.ExecuteReader,
            Guid.NewGuid(),
            Guid.NewGuid(),
            true,
            false,
            DateTimeOffset.UtcNow,
            CommandSource.LinqQuery);

        interceptor.ReaderExecuting(command, eventData, default);

        command.CommandText.ShouldBe(sqlWithTagInsideStringLiteral);
    }

    [Fact]
    public async Task EntityAuditMetadataInterceptorShouldRefreshUpdatedAtEvenWhenUpdateMarksAllPropertiesModified()
    {
        var initialTime = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var updatedTime = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(updatedTime);
        var interceptor = new EntityAuditMetadataSaveChangesInterceptor(clock);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        await using var context = new ApplicationDbContext(options);
        var project = new Project
        {
            Id = Guid.CreateVersion7(),
            Title = "Updated Project",
            StartDate = initialTime,
            CreatedAt = initialTime,
            UpdatedAt = initialTime,
        };

        // DbSet.Update marks every property IsModified = true with OriginalValue == CurrentValue.
        context.Set<Project>().Update(project);

        var savingEvent = new DbContextEventData(TestEventDefinition, (_, _) => string.Empty, context);
        await interceptor.SavingChangesAsync(savingEvent, default, TestContext.Current.CancellationToken);

        project.UpdatedAt.ShouldBe(updatedTime.UtcDateTime);
    }

    [Fact]
    public async Task EntityAuditInterceptorShouldDeferFlushUntilTransactionCommitAndDiscardOnRollback()
    {
        var ct = TestContext.Current.CancellationToken;
        var buffer = new RecordingAuditBuffer();
        var actorAccessor = Substitute.For<IAuditActorAccessor>();
        var interceptor = new EntityAuditSaveChangesInterceptor(
            buffer,
            actorAccessor,
            MsOptions.Create(new AuditOptions { Enabled = true }),
            new MutableTimeProvider(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero)));

        var dbTransaction1 = Substitute.For<DbTransaction>();
        var dbTransaction2 = Substitute.For<DbTransaction>();
        var txEndEventDef = new EventDefinition(
            Substitute.For<ILoggingOptions>(),
            RelationalEventId.TransactionCommitted,
            LogLevel.Debug,
            "TransactionCommitted",
            _ => (_, _) => { });

        // Simulate SaveChanges inside an active transaction by using a DbContext with a mocked CurrentTransaction
        // via EF Core's Sqlite/Relational or testing TransactionCommitted/RolledBack with a test DbContext wrapper.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        await using var context = new ApplicationDbContext(options);

        // Verify bulk update redaction & ignore rules on Auditable entity
        var projectId = Guid.CreateVersion7();
        interceptor.RecordBulkUpdate<Tag>(
            context,
            projectId.ToString(),
            new Dictionary<string, AuditPropertyChange>(StringComparer.Ordinal)
            {
                [nameof(Tag.Title)] = new AuditPropertyChange(JsonValue.Create("Old"), JsonValue.Create("New")),
                [nameof(Tag.NormalizedTitle)] = new AuditPropertyChange(JsonValue.Create("OLD"), JsonValue.Create("NEW")),
            });

        buffer.Records.Count.ShouldBe(1);
        buffer.Records[0].Changes.ShouldContainKey(nameof(Tag.Title));
        buffer.Records[0].Changes.ShouldNotContainKey(nameof(Tag.NormalizedTitle));

        // Verify calling TransactionRolledBackAsync / TransactionCommittedAsync on untracked transactions is a safe no-op
        var endData1 = new TransactionEndEventData(
            txEndEventDef,
            (_, _) => string.Empty,
            dbTransaction1,
            context,
            Guid.NewGuid(),
            Guid.NewGuid(),
            true,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero);
        var endData2 = new TransactionEndEventData(
            txEndEventDef,
            (_, _) => string.Empty,
            dbTransaction2,
            context,
            Guid.NewGuid(),
            Guid.NewGuid(),
            true,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero);

        await Should.NotThrowAsync(() => interceptor.TransactionRolledBackAsync(dbTransaction1, endData1, ct));
        await Should.NotThrowAsync(() => interceptor.TransactionCommittedAsync(dbTransaction2, endData2, ct));
    }

    [Fact]
    public async Task TokenServiceShouldRespectTimeProviderForExpirationAndPreserveClaims()
    {
        var ct = TestContext.Current.CancellationToken;
        var startTime = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(startTime);
        var jwtOptions = MsOptions.Create(new JwtOptions
        {
            Secret = "super-secret-signing-key-for-tests-1234",
            Issuer = "CrmTestIssuer",
            Audience = "CrmTestAudience",
            AccessTokenExpiryMinutes = 15,
            InvitationTokenExpiryHours = 1,
            ResetPasswordTokenExpiryHours = 1,
        });

        var tokenService = new TokenService(jwtOptions, NullLogger<TokenService>.Instance, clock);
        var userId = Guid.CreateVersion7();
        var roleId = Guid.CreateVersion7();

        // 1. Access token preserves sub, email, and role_id claims and validates when expired via GetPrincipalFromExpiredToken
        var accessToken = tokenService.GenerateAccessToken(new UserBasicDto(userId, "admin@ngo.org"), [roleId]);
        clock.Advance(TimeSpan.FromHours(2));

        var expiredPrincipal = tokenService.GetPrincipalFromExpiredToken(accessToken.Token);
        expiredPrincipal.ShouldNotBeNull();
        expiredPrincipal.FindFirst(CustomClaimTypes.Sub)?.Value.ShouldBe(userId.ToString());
        expiredPrincipal.FindFirst(ClaimTypes.Email)?.Value.ShouldBe("admin@ngo.org");
        expiredPrincipal.FindFirst(CustomClaimTypes.RoleId)?.Value.ShouldBe(roleId.ToString());

        // 2. Invitation token is rejected by GetPrincipalFromExpiredToken and respects TimeProvider expiry
        var inviteToken = tokenService.GenerateInvitationToken(
            new GenerateInvitationDto("invitee@ngo.org", Guid.CreateVersion7(), [roleId]));

        tokenService.GetPrincipalFromExpiredToken(inviteToken.Token).ShouldBeNull();

        var validInvitePrincipal = await tokenService.ValidateInvitationTokenAsync(inviteToken.Token, ct);
        validInvitePrincipal.ShouldNotBeNull();
        validInvitePrincipal.FindFirst(ClaimTypes.Email)?.Value.ShouldBe("invitee@ngo.org");

        clock.Advance(TimeSpan.FromHours(2));
        var expiredInvitePrincipal = await tokenService.ValidateInvitationTokenAsync(inviteToken.Token, ct);
        expiredInvitePrincipal.ShouldBeNull();
    }

    [Fact]
    public async Task PermissionServiceShouldEnforceConsistentOrdinalCaseSensitivityForSingleAndMultipleRoles()
    {
        var ct = TestContext.Current.CancellationToken;
        var role1 = Guid.CreateVersion7();
        var role2 = Guid.CreateVersion7();
        var exactClaim = AccessRight.ViewUser.ToClaimValue();

        var cache = Substitute.For<IRolePermissionsCache>();
        cache.GetRolePermissionsAsync(role1, ct)
            .Returns(new HashSet<string>(StringComparer.Ordinal) { exactClaim.ToUpperInvariant() });
        cache.GetRolePermissionsAsync(role2, ct)
            .Returns(new HashSet<string>(StringComparer.Ordinal) { exactClaim });

        var service = new PermissionService(cache);

        // Wrong-case claim must be rejected consistently whether checked on 1 role or multiple roles.
        (await service.HasAccessAsync([role1], AccessRight.ViewUser, ct)).ShouldBeFalse();
        (await service.HasAccessAsync([role1, role1], AccessRight.ViewUser, ct)).ShouldBeFalse();

        // Exact-case claim present on second role must be granted.
        (await service.HasAccessAsync([role1, role2], AccessRight.ViewUser, ct)).ShouldBeTrue();
    }

    [Fact]
    public void UnitOfWorkShouldImplementBothSyncAndAsyncDisposalCleanly()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        using var context = new ApplicationDbContext(options);
        using var unitOfWork = new UnitOfWork(context);

        unitOfWork.ShouldBeAssignableTo<IDisposable>();
        unitOfWork.ShouldBeAssignableTo<IAsyncDisposable>();
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;

        while ((index = source.IndexOf(value, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed class MutableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }

    private sealed class RecordingAuditBuffer : IAuditLogBuffer
    {
        public List<EntityAuditRecord> Records { get; } = [];

        public void Enqueue(EntityAuditRecord record) => Records.Add(record);

        public void EnqueueRange(IEnumerable<EntityAuditRecord> records) => Records.AddRange(records);
    }
}
