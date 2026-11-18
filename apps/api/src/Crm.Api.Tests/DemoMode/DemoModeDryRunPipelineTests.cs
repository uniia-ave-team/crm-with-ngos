using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Crm.Api.Controllers.V1;
using Crm.Api.DemoMode;
using Crm.Api.Dtos;
using Crm.Api.Extensions;
using Crm.Api.Middleware;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.Common;
using Crm.Application.Dtos.CustomTables;
using Crm.Application.Dtos.CustomTables.Commands;
using Crm.Application.Dtos.Ngo;
using Crm.Application.Dtos.Ngo.Commands;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.Role.Commands;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Extensions;
using Crm.Application.Features.CustomTables.Mappings;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Options;
using Crm.Infrastructure.Services;
using Mapster;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Crm.Api.Tests.DemoMode;

/// <summary>
/// Full HTTP + DemoMediator + TransactionalBehavior + DemoUnitOfWork + DemoUserRefreshTokenRepository
/// + DemoFileStorageService integration tests for Demo Mode (running in-memory via <see cref="TestServer"/>
/// without external database dependencies).
/// </summary>
public class DemoModeDryRunPipelineTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private string _tempUploadDir = string.Empty;

    static DemoModeDryRunPipelineTests()
    {
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        new CustomTableMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    public ValueTask InitializeAsync()
    {
        _tempUploadDir = Path.Combine(Path.GetTempPath(), $"crm-demo-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempUploadDir);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (!string.IsNullOrEmpty(_tempUploadDir) && Directory.Exists(_tempUploadDir))
        {
            Directory.Delete(_tempUploadDir, recursive: true);
        }

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task DemoModeFullPipelineShouldKeepDbReadOnlyAuthenticateInMemorySafelyInterceptIdentityAndHonorBlockedPaths()
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = new InMemoryDemoHarness(_tempUploadDir);

        await using var app = await harness.BuildAndStartAppAsync(
            new Dictionary<string, string?>
            {
                [$"{DemoModeOptions.Position}:{nameof(DemoModeOptions.Enabled)}"] = "true",
                [$"{DemoModeOptions.Position}:{nameof(DemoModeOptions.DemoBlockedPaths)}:0"] = "api/v1/custom-blocked-prefix/",
            },
            ct);

        using var client = app.GetTestClient();

        // 1. Auth endpoints succeed end-to-end using bounded in-memory DemoRefreshTokenStore with ZERO DB commits
        var loginResp = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new AuthenticateUserCommand(InMemoryDemoHarness.UserEmail, "StrongPass1!"),
            ct);
        loginResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var loginTokens = await loginResp.Content.ReadFromJsonAsync<AuthTokensDto>(JsonOptions, ct);
        loginTokens.ShouldNotBeNull();
        loginTokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        loginTokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        harness.CommittedRefreshTokens.ShouldBeEmpty();

        var refreshResp = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh-token",
            new RefreshTokensCommand(loginTokens.AccessToken, loginTokens.RefreshToken),
            ct);
        refreshResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var refreshedTokens = await refreshResp.Content.ReadFromJsonAsync<AuthTokensDto>(JsonOptions, ct);
        refreshedTokens.ShouldNotBeNull();
        refreshedTokens.RefreshToken.ShouldNotBe(loginTokens.RefreshToken);
        harness.CommittedRefreshTokens.ShouldBeEmpty();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

        var logoutResp = await client.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new LogoutRequest(refreshedTokens.RefreshToken),
            ct);
        logoutResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Old rotated/logged-out refresh token is rejected by in-memory store
        var refreshAfterLogoutResp = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh-token",
            new RefreshTokensCommand(refreshedTokens.AccessToken, refreshedTokens.RefreshToken),
            ct);
        refreshAfterLogoutResp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        harness.UnitOfWork.CommitCount.ShouldBe(0);

        // 2a. Dry-run entity creation returns real valid Guid, rolls back DB, and follow-up delete on created ID succeeds
        var createTableResp = await client.PostAsJsonAsync(
            "/api/v1/customtables",
            new CreateCustomTableCommand(
                "Donors",
                "Demo table",
                [new CreateCustomFieldDto("Full Name", "full_name", FieldDataType.Text, true, 0)]),
            ct);
        createTableResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdTableId = await createTableResp.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);
        createdTableId.ShouldNotBe(Guid.Empty);
        harness.CommittedCustomTables.ShouldNotContain(t => t.Id == createdTableId);

        var deleteDryRunTableResp = await client.DeleteAsync($"/api/v1/customtables/{createdTableId}", ct);
        deleteDryRunTableResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var deleteUnknownTableResp = await client.DeleteAsync($"/api/v1/customtables/{Guid.NewGuid()}", ct);
        deleteUnknownTableResp.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // 2b. Dry-run file upload via NgoController.UploadLogo validates image, returns 200 OK, and writes nothing to disk
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", "ngo-logo.png");

        var uploadResp = await client.PostAsync("/api/v1/ngo/logo", form, ct);
        uploadResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        Directory.GetFiles(_tempUploadDir, "*", SearchOption.AllDirectories).ShouldBeEmpty();

        // 3. Sensitive identity & token commands are safely intercepted (no DB DML / no real signed reset or invite JWTs)
        var changePasswordResp = await client.PostAsJsonAsync(
            "/api/v1/users/me/password",
            new UpdatePasswordRequest("StrongPass1!", "NewPass1!"),
            ct);
        changePasswordResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await harness.IdentityService.DidNotReceive().ChangePasswordAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        var wrongCurrentPasswordResp = await client.PostAsJsonAsync(
            "/api/v1/users/me/password",
            new UpdatePasswordRequest("WrongPass1!", "NewPass1!"),
            ct);
        wrongCurrentPasswordResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var deleteMeResp = await client.DeleteAsync("/api/v1/users/me", ct);
        deleteMeResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await harness.IdentityService.DidNotReceive().DeleteUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        var resetTokenResp = await client.PostAsync($"/api/v1/users/{harness.UserId}/password-reset-token", null, ct);
        resetTokenResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resetTokenDto = await resetTokenResp.Content.ReadFromJsonAsync<TokenResultDto>(JsonOptions, ct);
        resetTokenDto.ShouldNotBeNull();
        resetTokenDto.Token.ShouldStartWith("demo-reset-token-");
        await harness.IdentityService.DidNotReceive().GetResetTokenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        var inviteResp = await client.PostAsJsonAsync(
            "/api/v1/invitations",
            new InviteUserCommand("new-colleague@ngo.org", [harness.RoleId]),
            ct);
        inviteResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var inviteTokenDto = await inviteResp.Content.ReadFromJsonAsync<TokenResultDto>(JsonOptions, ct);
        inviteTokenDto.ShouldNotBeNull();
        inviteTokenDto.Token.ShouldStartWith("demo-invite-token-");

        // Synthetic demo invite token cannot be used to register a phantom user on /invitations/accept
        var acceptDemoInviteResp = await client.PostAsJsonAsync(
            "/api/v1/invitations/accept",
            new CompleteRegistrationCommand(inviteTokenDto.Token, "Jane", "Doe", null, "StrongPass1!", "StrongPass1!"),
            ct);
        acceptDemoInviteResp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // 4. Regular domain writes execute in dry-run rollback
        var updateNgoResp = await client.PutAsJsonAsync(
            "/api/v1/ngo",
            new UpdateNgoCommand("Updated Demo NGO"),
            ct);
        updateNgoResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var assignRoleResp = await client.PostAsync(
            $"/api/v1/users/{harness.UserId}/roles/{harness.RoleId}",
            null,
            ct);
        assignRoleResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var removeRoleResp = await client.DeleteAsync(
            $"/api/v1/users/{harness.UserId}/roles/{harness.RoleId}",
            ct);
        removeRoleResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var createRoleResp = await client.PostAsJsonAsync(
            "/api/v1/roles",
            new CreateRoleCommand("Coordinator", "Координаторка", "Координатори"),
            ct);
        createRoleResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdRoleElement = await createRoleResp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
        var dryRunRoleId = createdRoleElement.GetProperty("id").GetGuid();
        dryRunRoleId.ShouldNotBe(Guid.Empty);

        // Multi-step role creation: adding a claim to a dry-run role ID (which was rolled back) succeeds with 204
        var addClaimResp = await client.PostAsync(
            $"/api/v1/roles/{dryRunRoleId}/claims/{AccessRight.ViewUser.ToClaimValue()}",
            null,
            ct);
        addClaimResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        harness.UnitOfWork.CommitCount.ShouldBe(0);

        // Explicitly configured DemoBlockedPaths return 403 Forbidden (application/problem+json, DEMO_MODE_RESTRICTED)
        var blockedResp = await client.PostAsync("/api/v1/custom-blocked-prefix/item", null, ct);
        blockedResp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        blockedResp.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var blockedJson = await blockedResp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
        blockedJson.GetProperty("status").GetInt32().ShouldBe(403);
        blockedJson.GetProperty("errorCode").GetString().ShouldBe("DEMO_MODE_RESTRICTED");

        var getNgoResp = await client.GetAsync("/api/v1/ngo", ct);
        getNgoResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var getRolesResp = await client.GetAsync("/api/v1/roles", ct);
        getRolesResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 5. POST to a non-existent route returns 404 Not Found
        var notFoundResp = await client.PostAsync("/api/v1/nonexistent-endpoint", null, ct);
        notFoundResp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private sealed class InMemoryDemoHarness(string uploadDir)
    {
        public const string UserEmail = "demo@ngo.org";

        private readonly List<CustomTable> _stagedCustomTables = [];
        private readonly HashSet<string> _stagedAddedTokens = [];
        private readonly HashSet<string> _stagedRemovedTokens = [];

        public Guid NgoId { get; } = Guid.NewGuid();

        public Guid UserId { get; } = Guid.NewGuid();

        public Guid RoleId { get; } = Guid.NewGuid();

        public TrackingUnitOfWork UnitOfWork { get; } = new();

        public IIdentityService IdentityService { get; private set; } = null!;

        public List<CustomTable> CommittedCustomTables { get; } = [];

        public HashSet<string> CommittedRefreshTokens { get; } = [];

        public async Task<WebApplication> BuildAndStartAppAsync(
            IDictionary<string, string?> configOverrides,
            CancellationToken ct)
        {
            UnitOfWork.OnBegin = () =>
            {
                _stagedCustomTables.Clear();
                _stagedAddedTokens.Clear();
                _stagedRemovedTokens.Clear();
            };
            UnitOfWork.OnCommit = () =>
            {
                CommittedCustomTables.AddRange(_stagedCustomTables);
                CommittedRefreshTokens.UnionWith(_stagedAddedTokens);
                CommittedRefreshTokens.ExceptWith(_stagedRemovedTokens);
                _stagedCustomTables.Clear();
                _stagedAddedTokens.Clear();
                _stagedRemovedTokens.Clear();
            };
            UnitOfWork.OnRollback = () =>
            {
                _stagedCustomTables.Clear();
                _stagedAddedTokens.Clear();
                _stagedRemovedTokens.Clear();
            };

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();

            var settings = new Dictionary<string, string?>(configOverrides)
            {
                ["JwtOptions:Secret"] = "integration-test-jwt-secret-key-0123456789abcdef",
                ["JwtOptions:Issuer"] = "CrmApi",
                ["JwtOptions:Audience"] = "CrmClient",
                ["FileStorage:UploadDirectory"] = uploadDir,
                ["FileStorage:BaseUrl"] = "http://localhost",
            };
            builder.Configuration.AddInMemoryCollection(settings);

            builder.Services.Configure<RouteOptions>(o => o.LowercaseUrls = true);
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(AuthController).Assembly);
            builder.Services.AddCustomApiVersioning();
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            builder.Services.AddProblemDetails();

            builder.Services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            builder.Services.AddCustomAuthorization();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddApplicationServices();

            builder.Services.AddOptions<JwtOptions>()
                .Bind(builder.Configuration.GetSection(JwtOptions.Position));
            builder.Services.AddOptions<FileStorageOptions>()
                .Bind(builder.Configuration.GetSection(FileStorageOptions.Position));

            var imageValidator = Substitute.For<IImageValidator>();
            imageValidator.Validate(Arg.Any<Stream>(), Arg.Any<string>())
                .Returns(_ => new FileValidationResult(IsValid: true));

            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<ITokenService, TokenService>();
            builder.Services.AddSingleton<IFileUrlProvider, LocalFileUrlProvider>();
            builder.Services.AddSingleton(imageValidator);
            builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
            builder.Services.AddScoped<IFileTransactionTracker, FileTransactionTracker>();
            builder.Services.AddSingleton<IUnitOfWork>(UnitOfWork);

            var currentUserService = Substitute.For<ICurrentUserService>();
            currentUserService.GetUserId().Returns(UserId);
            currentUserService.GetRoleIds().Returns([RoleId]);
            builder.Services.AddScoped(_ => currentUserService);

            var permissionService = Substitute.For<IPermissionService>();
            permissionService.HasAccessAsync(Arg.Any<List<Guid>>(), Arg.Any<AccessRight>(), Arg.Any<CancellationToken>())
                .Returns(true);
            builder.Services.AddScoped(_ => permissionService);

            RegisterAuthAndDomainStubs(builder.Services);

            // Register Demo Mode last so it wraps IMediator, IUnitOfWork, IUserRefreshTokenRepository,
            // IFileTransactionTracker, IFileStorageService, and IRolePermissionsCache
            builder.Services.AddDemoMode(builder.Configuration);

            var app = builder.Build();
            app.UseExceptionHandler();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseDemoMode();
            app.MapControllers();

            await app.StartAsync(ct);
            return app;
        }

        private void RegisterAuthAndDomainStubs(IServiceCollection services)
        {
            IdentityService = Substitute.For<IIdentityService>();
            IdentityService.CheckPasswordAsync(UserEmail, "StrongPass1!", Arg.Any<CancellationToken>())
                .Returns(true);

            var authUserRepo = Substitute.For<IAuthUserRepository>();
            authUserRepo.GetAsync<UserStatusDto>(UserEmail, Arg.Any<CancellationToken>())
                .Returns(new UserStatusDto(UserId, UserEmail, true));
            authUserRepo.GetAsync<UserBasicDto>(UserId, Arg.Any<CancellationToken>())
                .Returns(new UserBasicDto(UserId, UserEmail));

            var userRepo = Substitute.For<IUserRepository>();
            userRepo.IsActiveAsync(UserId, Arg.Any<CancellationToken>()).Returns(true);

            var authRoleRepo = Substitute.For<IAuthRoleRepository>();
            authRoleRepo.GetRoleIdsByUserAsync(UserId, Arg.Any<CancellationToken>()).Returns([RoleId]);
            authRoleRepo.GetPagedAsync<RoleDto>(
                    Arg.Any<string?>(),
                    Arg.Any<string?>(),
                    Arg.Any<string?>(),
                    Arg.Any<int>(),
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>())
                .Returns(new PagedResult<RoleDto>([], 0, 1, 10));
            authRoleRepo.GetAsync<RoleUserDto>(RoleId, Arg.Any<CancellationToken>())
                .Returns(new RoleUserDto(RoleId, "Editor"));
            authRoleRepo.GetAsync<RoleUserDto>(Arg.Is<Guid>(id => id != RoleId), Arg.Any<CancellationToken>())
                .Returns<RoleUserDto>(_ => throw new EntityNotFoundException<RoleUserDto>(Guid.Empty));

            var refreshTokenRepo = Substitute.For<IUserRefreshTokenRepository>();
            refreshTokenRepo.IsValidTokenAsync(UserId, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ci => CommittedRefreshTokens.Contains(ci.ArgAt<string>(1)));
            refreshTokenRepo.CreateAsync(UserId, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    _stagedAddedTokens.Add(ci.ArgAt<string>(1));
                    return Task.CompletedTask;
                });
            refreshTokenRepo.RemoveTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var token = ci.ArgAt<string>(0);
                    _stagedRemovedTokens.Add(token);
                    if (!UnitOfWork.InTransaction)
                    {
                        CommittedRefreshTokens.Remove(token);
                    }

                    return Task.CompletedTask;
                });

            var customTableRepo = Substitute.For<ICustomTableRepository>();
            var idProp = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!;
            customTableRepo.CreateAsync(Arg.Any<CustomTable>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var table = ci.Arg<CustomTable>();
                    if (table.Id == Guid.Empty)
                    {
                        idProp.SetValue(table, Guid.NewGuid());
                    }

                    _stagedCustomTables.Add(table);
                    return table;
                });
            customTableRepo.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(ci => Task.FromException(new EntityNotFoundException<CustomTable>(ci.Arg<Guid>())));

            var ngoRepo = Substitute.For<INgoRepository>();
            ngoRepo.GetIdAsync(Arg.Any<CancellationToken>()).Returns(NgoId);
            ngoRepo.GetForUpdateAsync(Arg.Any<CancellationToken>())
                .Returns(new Ngo { Name = "Test NGO", Logo = "old-logo.png" });
            ngoRepo.GetAsync<NgoDto>(Arg.Any<CancellationToken>())
                .Returns(new NgoDto(NgoId, "Test NGO", null, DateTime.UtcNow));

            var roleIdentityService = Substitute.For<IRoleIdentityService>();
            roleIdentityService.CreateRoleAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(_ => Guid.NewGuid());

            var optionService = Substitute.For<IOptionService>();
            optionService.GetIdentityOptions().Returns(new IdentityOptionsDto(8, true, true, true, true, true));

            // IdentityService.AddRefreshTokenAsync delegates to the decorated IUserRefreshTokenRepository in production;
            // wire the stub similarly via DI resolution so DemoUserRefreshTokenRepository receives login tokens.
            services.AddScoped(sp =>
            {
                var tokenRepo = sp.GetRequiredService<IUserRefreshTokenRepository>();
                IdentityService.AddRefreshTokenAsync(UserId, Arg.Any<TokenResult>(), Arg.Any<CancellationToken>())
                    .Returns(ci => tokenRepo.CreateAsync(UserId, ci.Arg<TokenResult>().Token, ci.Arg<TokenResult>().ExpiresAt, ci.Arg<CancellationToken>()));
                return IdentityService;
            });

            services.AddScoped(_ => authUserRepo);
            services.AddScoped(_ => userRepo);
            services.AddScoped(_ => authRoleRepo);
            services.AddScoped(_ => refreshTokenRepo);
            services.AddScoped(_ => customTableRepo);
            services.AddScoped(_ => ngoRepo);
            services.AddScoped(_ => roleIdentityService);
            services.AddSingleton(Substitute.For<IRolePermissionsCache>());
            services.AddScoped(_ => Substitute.For<IUserCustomFieldRepository>());
            services.AddScoped(_ => Substitute.For<ILoginPageImageRepository>());
            services.AddScoped(_ => Substitute.For<ITableTemplateRepository>());
            services.AddScoped(_ => Substitute.For<ICustomRecordRepository>());
            services.AddScoped(_ => Substitute.For<ICustomFieldRepository>());
            services.AddScoped(_ => optionService);
        }
    }

    private sealed class TrackingUnitOfWork : IUnitOfWork
    {
        public bool InTransaction { get; private set; }

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public Action? OnBegin { get; set; }

        public Action? OnCommit { get; set; }

        public Action? OnRollback { get; set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            InTransaction = true;
            OnBegin?.Invoke();
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            InTransaction = false;
            OnCommit?.Invoke();
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            InTransaction = false;
            OnRollback?.Invoke();
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
