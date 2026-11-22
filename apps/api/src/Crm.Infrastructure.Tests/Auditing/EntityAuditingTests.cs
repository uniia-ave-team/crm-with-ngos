using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Crm.Domain.Attributes;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Crm.Infrastructure.Auditing;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Crm.Infrastructure.Tests.Auditing;

/// <summary>
/// Verifies the centralized attribute-driven entity auditing pipeline:
/// <see cref="AuditMetadataCache"/>, <see cref="EntityAuditSaveChangesInterceptor"/>,
/// <see cref="ChannelAuditLogBuffer"/>, <see cref="AuditLogBackgroundWriter"/>,
/// <see cref="HttpContextAuditActorAccessor"/>, <see cref="AuditLogEntry"/>,
/// and bulk audit helpers on <see cref="ApplicationDbContext"/>.
/// </summary>
public class EntityAuditingTests
{
    private const string DummyConnectionString =
        "Host=localhost;Database=crm_audit_unit_test;Username=postgres;Password=postgres";

    private static readonly EventDefinitionBase TestEventDefinition = new EventDefinition(
        Substitute.For<ILoggingOptions>(),
        CoreEventId.SaveChangesStarting,
        LogLevel.Debug,
        nameof(CoreEventId.SaveChangesStarting),
        _ => (_, _) => { });

    [Fact]
    public void AuditMetadataCacheShouldRespectAuditableIgnoreAndRedactAttributes()
    {
        AuditMetadataCache.TryGetMetadata(typeof(TaskReadState)).ShouldBeNull();
        AuditMetadataCache.TryGetMetadata(typeof(UserRefreshToken)).ShouldBeNull();
        AuditMetadataCache.TryGetMetadata(typeof(AuditLogEntry)).ShouldBeNull();

        var tagMeta = AuditMetadataCache.TryGetMetadata(typeof(Tag));
        tagMeta.ShouldNotBeNull();
        tagMeta.EntityName.ShouldBe(nameof(Tag));
        tagMeta.IgnoredProperties.ShouldContain(nameof(Tag.NormalizedTitle));

        var customMeta = AuditMetadataCache.TryGetMetadata(typeof(CustomNamedAuditedEntity));
        customMeta.ShouldNotBeNull();
        customMeta.EntityName.ShouldBe("SecretItem");
        customMeta.IgnoredProperties.ShouldContain(nameof(CustomNamedAuditedEntity.IgnoredNote));
        customMeta.RedactedProperties.ShouldContain(nameof(CustomNamedAuditedEntity.SecretCode));
    }

    [Fact]
    public async Task InterceptorShouldCaptureCreatedUpdatedAndDeletedForAuditableEntitiesAndSkipNonAuditable()
    {
        var actorId = Guid.CreateVersion7();
        var fixedNow = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var buffer = new RecordingAuditBuffer();
        var interceptor = CreateInterceptor(buffer, actorId, "trace-123", fixedNow);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        await using var context = new ApplicationDbContext(options);

        // 1. Created: auditable Tag + non-auditable UserRefreshToken in the same save
        var tagId = Guid.CreateVersion7();
        var projectId = Guid.CreateVersion7();
        var tag = new Tag { Id = tagId, ProjectId = projectId }.SetTitle("Urgent");
        var token = new UserRefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = actorId,
            Token = "token-value",
            ExpiresAt = fixedNow.AddDays(1),
        };

        context.Set<Tag>().Add(tag);
        context.Set<UserRefreshToken>().Add(token);

        await SimulateSaveSuccessAsync(interceptor, context, TestContext.Current.CancellationToken);

        buffer.Records.Count.ShouldBe(1);
        var created = buffer.Records[0];
        created.EntityType.ShouldBe(nameof(Tag));
        created.EntityKey.ShouldBe(tagId.ToString());
        created.Action.ShouldBe(AuditAction.Created);
        created.ActorUserId.ShouldBe(actorId);
        created.TraceId.ShouldBe("trace-123");
        created.OccurredAtUtc.ShouldBe(fixedNow);
        created.Changes.ShouldContainKey(nameof(Tag.Title));
        created.Changes[nameof(Tag.Title)].OldValue.ShouldBeNull();
        created.Changes[nameof(Tag.Title)].NewValue!.GetValue<string>().ShouldBe("Urgent");
        created.Changes.ShouldNotContainKey(nameof(Tag.NormalizedTitle));

        // 2. Updated: modify Title, verify only Title is captured
        buffer.Records.Clear();
        tag.SetTitle("Critical");
        context.ChangeTracker.DetectChanges();

        await SimulateSaveSuccessAsync(interceptor, context, TestContext.Current.CancellationToken);

        buffer.Records.Count.ShouldBe(1);
        var updated = buffer.Records[0];
        updated.Action.ShouldBe(AuditAction.Updated);
        updated.Changes.Count.ShouldBe(1);
        updated.Changes[nameof(Tag.Title)].OldValue!.GetValue<string>().ShouldBe("Urgent");
        updated.Changes[nameof(Tag.Title)].NewValue!.GetValue<string>().ShouldBe("Critical");

        // 3. No-op modification: setting same value produces 0 audit records
        buffer.Records.Clear();
        context.Entry(tag).Property(t => t.Title).IsModified = true;

        await SimulateSaveSuccessAsync(interceptor, context, TestContext.Current.CancellationToken);

        buffer.Records.ShouldBeEmpty();

        // 4. Deleted: captures original values and key before detach
        buffer.Records.Clear();
        context.Entry(tag).State = EntityState.Deleted;

        await SimulateSaveSuccessAsync(interceptor, context, TestContext.Current.CancellationToken);

        buffer.Records.Count.ShouldBe(1);
        var deleted = buffer.Records[0];
        deleted.Action.ShouldBe(AuditAction.Deleted);
        deleted.EntityKey.ShouldBe(tagId.ToString());
        deleted.Changes[nameof(Tag.Title)].OldValue!.GetValue<string>().ShouldBe("Critical");
        deleted.Changes[nameof(Tag.Title)].NewValue.ShouldBeNull();
    }

    [Fact]
    public async Task InterceptorShouldComputeKeyByKeyDiffForJsonObjectColumns()
    {
        var buffer = new RecordingAuditBuffer();
        var interceptor = CreateInterceptor(buffer, Guid.CreateVersion7(), "trace-json", DateTimeOffset.UtcNow);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        await using var context = new ApplicationDbContext(options);

        var recordId = Guid.CreateVersion7();
        var record = new CustomRecord
        {
            Id = recordId,
            CustomTableId = Guid.CreateVersion7(),
            Data = new JsonObject
            {
                ["email"] = "old@ngo.org",
                ["legacy"] = "remove-me",
                ["unchanged"] = 42,
            },
        };

        context.Attach(record);
        record.Data = new JsonObject
        {
            ["email"] = "new@ngo.org",
            ["phone"] = "+380501234567",
            ["unchanged"] = 42,
        };
        context.ChangeTracker.DetectChanges();

        await SimulateSaveSuccessAsync(interceptor, context, TestContext.Current.CancellationToken);

        buffer.Records.Count.ShouldBe(1);
        var diff = buffer.Records[0].Changes;
        diff.ShouldContainKey("Data.email");
        diff["Data.email"].OldValue!.GetValue<string>().ShouldBe("old@ngo.org");
        diff["Data.email"].NewValue!.GetValue<string>().ShouldBe("new@ngo.org");

        diff.ShouldContainKey("Data.phone");
        diff["Data.phone"].OldValue.ShouldBeNull();
        diff["Data.phone"].NewValue!.GetValue<string>().ShouldBe("+380501234567");

        diff.ShouldContainKey("Data.legacy");
        diff["Data.legacy"].OldValue!.GetValue<string>().ShouldBe("remove-me");
        diff["Data.legacy"].NewValue.ShouldBeNull();

        diff.ShouldNotContainKey("Data.unchanged");
    }

    [Fact]
    public async Task InterceptorShouldRedactSensitivePropertiesAndUseCustomEntityName()
    {
        var buffer = new RecordingAuditBuffer();
        var interceptor = CreateInterceptor(buffer, null, null, DateTimeOffset.UtcNow);

        var options = new DbContextOptionsBuilder<TestAuditDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        await using var context = new TestAuditDbContext(options);

        var entity = new CustomNamedAuditedEntity
        {
            Id = Guid.CreateVersion7(),
            Name = "Alpha",
            SecretCode = "super-secret-1",
            IgnoredNote = "ignored-1",
        };

        context.Attach(entity);
        entity.Name = "Beta";
        entity.SecretCode = "super-secret-2";
        entity.IgnoredNote = "ignored-2";
        context.ChangeTracker.DetectChanges();

        await SimulateSaveSuccessAsync(interceptor, context, TestContext.Current.CancellationToken);

        buffer.Records.Count.ShouldBe(1);
        var audit = buffer.Records[0];
        audit.EntityType.ShouldBe("SecretItem");
        audit.Changes.ShouldContainKey(nameof(CustomNamedAuditedEntity.Name));
        audit.Changes[nameof(CustomNamedAuditedEntity.Name)].NewValue!.GetValue<string>().ShouldBe("Beta");
        audit.Changes.ShouldContainKey(nameof(CustomNamedAuditedEntity.SecretCode));
        audit.Changes[nameof(CustomNamedAuditedEntity.SecretCode)].OldValue!.GetValue<string>().ShouldBe("[REDACTED]");
        audit.Changes[nameof(CustomNamedAuditedEntity.SecretCode)].NewValue!.GetValue<string>().ShouldBe("[REDACTED]");
        audit.Changes.ShouldNotContainKey(nameof(CustomNamedAuditedEntity.IgnoredNote));
    }

    [Fact]
    public async Task InterceptorShouldFormatCompositePrimaryKeysAndDiscardOnSaveFailure()
    {
        var buffer = new RecordingAuditBuffer();
        var interceptor = CreateInterceptor(buffer, Guid.CreateVersion7(), null, DateTimeOffset.UtcNow);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        await using var context = new ApplicationDbContext(options);

        var projectId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var member = new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            RoleTitle = "Coordinator",
            Claim = ProjectClaim.Edit,
        };

        context.Set<ProjectMember>().Add(member);
        await SimulateSaveSuccessAsync(interceptor, context, TestContext.Current.CancellationToken);

        buffer.Records.Count.ShouldBe(1);
        buffer.Records[0].EntityKey.ShouldBe($"{projectId}:{userId}");
        buffer.Records[0].Changes[nameof(ProjectMember.Claim)].NewValue!.GetValue<string>()
            .ShouldBe(nameof(ProjectClaim.Edit));

        // Simulate SaveChanges failure -> must discard pending entries
        buffer.Records.Clear();
        context.Entry(member).State = EntityState.Unchanged;
        member.RoleTitle = "Lead";
        context.ChangeTracker.DetectChanges();

        var savingEvent = new DbContextEventData(TestEventDefinition, (_, _) => string.Empty, context);
        await interceptor.SavingChangesAsync(savingEvent, default, TestContext.Current.CancellationToken);
        var failedEvent = new DbContextErrorEventData(
            TestEventDefinition,
            (_, _) => string.Empty,
            context,
            new InvalidOperationException("DB write failed"));
        await interceptor.SaveChangesFailedAsync(failedEvent, TestContext.Current.CancellationToken);

        buffer.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task InterceptorShouldDoNothingWhenDisabledInOptions()
    {
        var buffer = new RecordingAuditBuffer();
        var interceptor = CreateInterceptor(buffer, Guid.CreateVersion7(), null, DateTimeOffset.UtcNow, enabled: false);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.Set<Tag>().Add(new Tag { Id = Guid.CreateVersion7(), ProjectId = Guid.CreateVersion7(), Title = "Test" });

        await SimulateSaveSuccessAsync(interceptor, context, TestContext.Current.CancellationToken);

        buffer.Records.ShouldBeEmpty();
    }

    [Fact]
    public void BulkAuditHelpersShouldRespectAuditableAttributeAndMapToAuditLogEntry()
    {
        var actorId = Guid.CreateVersion7();
        var buffer = new RecordingAuditBuffer();
        var interceptor = CreateInterceptor(buffer, actorId, "bulk-trace", DateTimeOffset.UtcNow);

        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        using var context = new ApplicationDbContext(dbOptions);

        var deletedId = Guid.CreateVersion7();
        context.TryEnqueueBulkDeleteAudit<Project>(deletedId.ToString());
        context.TryEnqueueBulkDeleteAudit<UserRefreshToken>(Guid.CreateVersion7().ToString());

        buffer.Records.Count.ShouldBe(1);
        var record = buffer.Records[0];
        record.EntityType.ShouldBe(nameof(Project));
        record.EntityKey.ShouldBe(deletedId.ToString());
        record.Action.ShouldBe(AuditAction.Deleted);
        record.ActorUserId.ShouldBe(actorId);
        record.TraceId.ShouldBe("bulk-trace");

        var recordWithChanges = record with
        {
            Changes = new Dictionary<string, AuditPropertyChange>(StringComparer.Ordinal)
            {
                ["Title"] = new AuditPropertyChange(JsonValue.Create("Old"), JsonValue.Create("New")),
            },
        };

        var entry = recordWithChanges.ToAuditLogEntry();
        entry.Id.ShouldBe(recordWithChanges.Id);
        entry.EntityType.ShouldBe(nameof(Project));
        entry.Changes["Title"]!["old"]!.GetValue<string>().ShouldBe("Old");
        entry.Changes["Title"]!["new"]!.GetValue<string>().ShouldBe("New");
    }

    [Fact]
    public void HttpContextAuditActorAccessorShouldResolveUserIdSafely()
    {
        var noContextAccessor = new HttpContextAuditActorAccessor(httpContextAccessor: null);
        noContextAccessor.GetActorUserId().ShouldBeNull();

        var userId = Guid.CreateVersion7();
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "http-trace-1",
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())])),
        };
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        httpAccessor.HttpContext.Returns(httpContext);

        var actorAccessor = new HttpContextAuditActorAccessor(httpAccessor);
        actorAccessor.GetActorUserId().ShouldBe(userId);
        actorAccessor.GetTraceId().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task BackgroundWriterShouldFlushBatchesAndSurviveSinkFailures()
    {
        var auditOptions = MsOptions.Create(new AuditOptions
        {
            QueueCapacity = 10,
            BatchSize = 2,
            FlushIntervalMilliseconds = 50,
        });

        var buffer = new ChannelAuditLogBuffer(auditOptions, NullLogger<ChannelAuditLogBuffer>.Instance);
        var writtenBatches = new List<IReadOnlyCollection<EntityAuditRecord>>();
        int callCount = 0;

        var sink = Substitute.For<IAuditLogSink>();
        sink.WriteBatchAsync(Arg.Any<IReadOnlyCollection<EntityAuditRecord>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                int current = Interlocked.Increment(ref callCount);
                var snapshot = callInfo.Arg<IReadOnlyCollection<EntityAuditRecord>>().ToList();
                if (current == 1)
                {
                    throw new InvalidOperationException("Transient sink failure");
                }

                lock (writtenBatches)
                {
                    writtenBatches.Add(snapshot);
                }

                return Task.CompletedTask;
            });

        using var writer = new AuditLogBackgroundWriter(
            buffer,
            sink,
            auditOptions,
            NullLogger<AuditLogBackgroundWriter>.Instance);

        buffer.Enqueue(CreateSampleRecord("e1"));
        buffer.Enqueue(CreateSampleRecord("e2"));
        buffer.Enqueue(CreateSampleRecord("e3"));
        buffer.Complete();

        await writer.StartAsync(TestContext.Current.CancellationToken);

        // Wait briefly for the background worker to drain the completed channel.
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            lock (writtenBatches)
            {
                if (writtenBatches.Count > 0)
                {
                    break;
                }
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        await writer.StopAsync(TestContext.Current.CancellationToken);

        callCount.ShouldBeGreaterThanOrEqualTo(2);
        writtenBatches.Count.ShouldBe(1);
        writtenBatches[0].Single().EntityKey.ShouldBe("e3");
    }

    [Fact]
    public void ApplicationDbContextShouldRegisterGlobalUtcValueConvertersForAllDateTimeAndDateTimeOffsetColumns()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        using var context = new ApplicationDbContext(options);

        var projectStartConverter = context.Model
            .FindEntityType(typeof(Project))!
            .FindProperty(nameof(Project.StartDate))!
            .GetValueConverter();

        projectStartConverter.ShouldNotBeNull();
        var convertedDt = (DateTime)projectStartConverter.ConvertToProvider(
            new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Unspecified))!;
        convertedDt.Kind.ShouldBe(DateTimeKind.Utc);

        var auditOccurredConverter = context.Model
            .FindEntityType(typeof(AuditLogEntry))!
            .FindProperty(nameof(AuditLogEntry.OccurredAtUtc))!
            .GetValueConverter();

        auditOccurredConverter.ShouldNotBeNull();
        var convertedDto = (DateTimeOffset)auditOccurredConverter.ConvertToProvider(
            new DateTimeOffset(2026, 11, 21, 14, 0, 0, TimeSpan.FromHours(2)))!;
        convertedDto.Offset.ShouldBe(TimeSpan.Zero);
        convertedDto.Hour.ShouldBe(12);
    }

    [Fact]
    public async Task MetadataInterceptorShouldPopulateUtcTimestampsJoinedAtAndAuthorIds()
    {
        var actorId = Guid.CreateVersion7();
        var fixedNow = new DateTimeOffset(2026, 11, 21, 18, 30, 0, TimeSpan.Zero);

        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(fixedNow);

        var actorAccessor = Substitute.For<IAuditActorAccessor>();
        actorAccessor.GetActorUserId().Returns(actorId);

        var metadataInterceptor = new EntityAuditMetadataSaveChangesInterceptor(timeProvider, actorAccessor);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(DummyConnectionString)
            .Options;

        await using var context = new ApplicationDbContext(options);

        var project = new Project { Title = "Alpha", StartDate = fixedNow.UtcDateTime };
        var member = new ProjectMember { ProjectId = Guid.CreateVersion7(), UserId = actorId };
        context.AddRange(project, member);

        var savingEvent = new DbContextEventData(TestEventDefinition, (_, _) => string.Empty, context);
        await metadataInterceptor.SavingChangesAsync(savingEvent, default, TestContext.Current.CancellationToken);

        project.CreatedAt.ShouldBe(fixedNow.UtcDateTime);
        project.UpdatedAt.ShouldBe(fixedNow.UtcDateTime);
        project.CreatedBy.ShouldBe(actorId);
        project.UpdatedBy.ShouldBe(actorId);
        member.JoinedAt.ShouldBe(fixedNow.UtcDateTime);
    }

    private static EntityAuditSaveChangesInterceptor CreateInterceptor(
        IAuditLogBuffer buffer,
        Guid? actorId,
        string? traceId,
        DateTimeOffset now,
        bool enabled = true)
    {
        var actorAccessor = Substitute.For<IAuditActorAccessor>();
        actorAccessor.GetActorUserId().Returns(actorId);
        actorAccessor.GetTraceId().Returns(traceId);

        var timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(now);

        return new EntityAuditSaveChangesInterceptor(
            buffer,
            actorAccessor,
            MsOptions.Create(new AuditOptions { Enabled = enabled }),
            timeProvider);
    }

    private static async Task SimulateSaveSuccessAsync(
        EntityAuditSaveChangesInterceptor interceptor,
        DbContext context,
        CancellationToken cancellationToken)
    {
        var savingEvent = new DbContextEventData(TestEventDefinition, (_, _) => string.Empty, context);
        await interceptor.SavingChangesAsync(savingEvent, default, cancellationToken);

        var savedEvent = new SaveChangesCompletedEventData(TestEventDefinition, (_, _) => string.Empty, context, 1);
        await interceptor.SavedChangesAsync(savedEvent, 1, cancellationToken);
        context.ChangeTracker.AcceptAllChanges();
    }

    private static EntityAuditRecord CreateSampleRecord(string key) => new(
        Guid.CreateVersion7(),
        DateTimeOffset.UtcNow,
        null,
        null,
        nameof(Project),
        key,
        AuditAction.Created,
        new Dictionary<string, AuditPropertyChange>(StringComparer.Ordinal));

    private sealed class RecordingAuditBuffer : IAuditLogBuffer
    {
        public List<EntityAuditRecord> Records { get; } = [];

        public void Enqueue(EntityAuditRecord record) => Records.Add(record);

        public void EnqueueRange(IEnumerable<EntityAuditRecord> records) => Records.AddRange(records);
    }

    [Auditable("SecretItem")]
    private sealed class CustomNamedAuditedEntity
    {
        public Guid Id { get; init; }

        public string Name { get; set; } = string.Empty;

        [AuditRedact]
        public string SecretCode { get; set; } = string.Empty;

        [AuditIgnore]
        public string IgnoredNote { get; set; } = string.Empty;
    }

    private sealed class TestAuditDbContext(DbContextOptions<TestAuditDbContext> options) : DbContext(options)
    {
        public DbSet<CustomNamedAuditedEntity> Items => Set<CustomNamedAuditedEntity>();
    }
}
