using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Crm.Api.DemoMode;
using Crm.Api.DemoMode.DemoData;
using Crm.Api.Services;
using Crm.Application.Extensions;
using Crm.Application.Features.System.Queries;
using Crm.Application.Features.Users.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Constants.Validation;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Crm.Infrastructure.DependencyInjection;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Extensions;
using Crm.Infrastructure.Persistence.Seeders.SystemPresets;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Crm.Api.Tests.DemoMode;

/// <summary>
/// Integration tests for <see cref="DemoDataSeeder"/> against a real PostgreSQL Testcontainer: the real
/// infrastructure and application services are registered, the migrations and the regular seeders run exactly
/// as at application startup, then the demo seeder fills the database.
/// The content-only tests at the bottom need no database and always run.
/// </summary>
public sealed partial class DemoDataSeederTests(DemoDataSeederTests.DemoDatabaseFixture fixture)
    : IClassFixture<DemoDataSeederTests.DemoDatabaseFixture>
{
    private const string DemoEmailSuffix = "@demo.example.org";
    private const int ExpectedDonorRecords = 15;
    private const int ExpectedVolunteerRecords = 12;
    private const int MinColleagues = 6;
    private const int MaxColleagues = 8;
    private const int MinTopicTables = 10;
    private const int MinTopicTableRows = 8;
    private const int MaxTopicTableRows = 20;

    [Fact]
    public async Task SeedAsyncShouldCompleteSystemSetup()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var status = await mediator.Send(new GetSystemSetupStatusQuery(), ct);

        status.HasAdmin.ShouldBeTrue();
        status.HasNgo.ShouldBeTrue();
        status.IsSetupComplete.ShouldBeTrue();

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ngo = await dbContext.Set<Ngo>().AsNoTracking().SingleAsync(ct);
        ngo.Name.ShouldNotBeNullOrWhiteSpace();
        (await dbContext.Set<User>().AsNoTracking().AllAsync(u => u.NgoId == ngo.Id, ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task SeedAsyncShouldCreateActiveDemoAccountsThatSignInWithTheirPasswordAndRole()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;

        foreach (var account in fixture.Accounts)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // The real login command: checks the password, the active flag and issues tokens.
            var tokens = await mediator.Send(new AuthenticateUserCommand(account.Email, account.Password), ct);
            tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
            tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();

            await Should.ThrowAsync<InvalidCredentialException>(async () =>
                await mediator.Send(new AuthenticateUserCommand(account.Email, account.Password + "x"), ct));

            var authUser = await dbContext.Users
                .AsNoTracking()
                .Include(u => u.UserProfile)
                .SingleAsync(u => u.Email == account.Email, ct);

            authUser.UserProfile.IsActive.ShouldBeTrue();
            authUser.UserProfile.FirstName.ShouldNotBeNullOrWhiteSpace();
            authUser.UserProfile.LastName.ShouldNotBeNullOrWhiteSpace();
            authUser.UserProfile.PhoneNumber.ShouldNotBeNullOrWhiteSpace();
            authUser.UserProfile.CustomFields.ShouldNotBeEmpty();

            var roleNames = await dbContext.Set<AuthUserRole>()
                .AsNoTracking()
                .Where(ur => ur.UserId == authUser.Id)
                .Select(ur => ur.Role.Name)
                .ToListAsync(ct);

            roleNames.ShouldBe([account.Role]);
        }
    }

    [Fact]
    public async Task SeedAsyncShouldCreateRolesWithSensiblePermissions()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();

        var adminRights = await GetRolePermissionsAsync(scope.ServiceProvider, RoleConstants.Admin, ct);
        var coordinatorRights = await GetRolePermissionsAsync(scope.ServiceProvider, DemoAccountOptions.CoordinatorRole, ct);
        var volunteerRights = await GetRolePermissionsAsync(scope.ServiceProvider, DemoAccountOptions.VolunteerRole, ct);

        adminRights.SetEquals(PermissionExtensions.GetAllStringValues()).ShouldBeTrue();

        coordinatorRights.ShouldContain(AccessRight.CreateCustomTable.ToClaimValue());
        coordinatorRights.ShouldContain(AccessRight.DeleteCustomRecord.ToClaimValue());
        coordinatorRights.ShouldContain(AccessRight.UpdateUser.ToClaimValue());
        coordinatorRights.ShouldNotContain(AccessRight.ManageRole.ToClaimValue());
        coordinatorRights.ShouldNotContain(AccessRight.AssignRoleToUser.ToClaimValue());
        coordinatorRights.ShouldNotContain(AccessRight.UpdateNgo.ToClaimValue());
        coordinatorRights.ShouldNotContain(AccessRight.DeleteUser.ToClaimValue());

        volunteerRights.ShouldContain(AccessRight.ViewCustomRecord.ToClaimValue());
        volunteerRights.ShouldContain(AccessRight.ViewUser.ToClaimValue());
        volunteerRights.ShouldNotContain(AccessRight.DeleteCustomRecord.ToClaimValue());
        volunteerRights.ShouldNotContain(AccessRight.UpdateCustomTable.ToClaimValue());
        volunteerRights.ShouldNotContain(AccessRight.CreateUser.ToClaimValue());
        volunteerRights.IsProperSubsetOf(coordinatorRights).ShouldBeTrue();
    }

    [Fact]
    public async Task SeedAsyncShouldCreateFictionalColleaguesWithFilledProfiles()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var users = await dbContext.Users
            .AsNoTracking()
            .Include(u => u.UserProfile)
            .Include(u => u.UserRoles)
            .ToListAsync(ct);

        // The shared schema of user custom fields: the demo names, one of them visible only with ViewCustomFields.
        List<UserCustomField> userFields = [.. await dbContext.Set<UserCustomField>().AsNoTracking().OrderBy(f => f.Order).ToListAsync(ct)];
        userFields.Select(f => f.Name).ShouldBe(DemoDataContent.CreateUserCustomFields().Select(f => f.Name));
        userFields.Count(f => !f.IsPublic).ShouldBe(1);

        var accountEmails = fixture.Accounts.Select(a => a.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var colleagues = users.Where(u => !accountEmails.Contains(u.Email!)).ToList();

        colleagues.Count.ShouldBeInRange(MinColleagues, MaxColleagues);
        colleagues.ShouldContain(u => !u.UserProfile.IsActive);
        colleagues.Count(u => u.UserProfile.IsActive).ShouldBeGreaterThan(colleagues.Count / 2);

        foreach (var user in users)
        {
            user.Email!.ShouldEndWith(DemoEmailSuffix);
            user.UserProfile.FirstName.ShouldNotBeNullOrWhiteSpace();
            user.UserProfile.LastName.ShouldNotBeNullOrWhiteSpace();
            FakePhoneRegex().IsMatch(user.UserProfile.PhoneNumber!).ShouldBeTrue();

            // Values are keyed by field id and match the schema (throws otherwise).
            user.UserProfile.CustomFields.ValidateAndBackfillPayload(userFields).Count.ShouldBe(userFields.Count);
            user.UserRoles.Count.ShouldBe(1);
        }

        // Colleagues exist only to populate the list: none of them shares a public demo password.
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        foreach (var colleague in colleagues)
        {
            foreach (var account in fixture.Accounts)
            {
                (await identityService.CheckPasswordAsync(colleague.Email!, account.Password, ct)).ShouldBeFalse();
            }
        }
    }

    [Fact]
    public async Task SeedAsyncShouldCreateTemplateBasedAndHandBuiltTablesWithValidRecords()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tables = await dbContext.Set<CustomTable>()
            .AsNoTracking()
            .Include(t => t.Fields)
            .Include(t => t.Records)
            .ToListAsync(ct);

        tables.Count.ShouldBe(2 + DemoTables.All.Count);

        // The fields of a template are stored with it (JSON), so they come without an Include.
        var systemTemplate = await dbContext.Set<TableTemplate>()
            .AsNoTracking()
            .SingleAsync(t => t.IsSystemPreset && t.Name == DemoDataContent.DonorTemplateName, ct);

        var donorTable = tables.Single(t => t.SourceTemplateId is not null);
        donorTable.SourceTemplateId.ShouldBe(systemTemplate.Id);
        donorTable.Fields.OrderBy(f => f.Order).Select(f => f.Name).ShouldBe(systemTemplate.Fields.OrderBy(f => f.Order).Select(f => f.Name));
        donorTable.Records.Count.ShouldBe(ExpectedDonorRecords);

        var volunteerTable = tables.Single(t => t.Name == DemoDataContent.VolunteerTableName);
        volunteerTable.Name.ShouldBe(DemoDataContent.VolunteerTableName);
        volunteerTable.Records.Count.ShouldBe(ExpectedVolunteerRecords);
        volunteerTable.Fields.OrderBy(f => f.Order).Select(f => f.Name).ShouldBe(DemoDataContent.CreateVolunteerFields().Select(f => f.Name));

        foreach (var definition in DemoTables.All)
        {
            var topicTable = tables.Single(t => t.Name == definition.Name);
            topicTable.SourceTemplateId.ShouldBeNull();
            topicTable.Description.ShouldBe(definition.Description);
            topicTable.Fields.OrderBy(f => f.Order).Select(f => f.Name).ShouldBe(definition.Columns.Select(c => c.Name));
            topicTable.Records.Count.ShouldBe(definition.CreateRows(DateTimeOffset.UtcNow).Count);
        }

        foreach (var table in tables)
        {
            List<CustomField> fields = [.. table.Fields.OrderBy(f => f.Order)];
            fields.EnsureValidFieldSchema();

            // The manual order (positions) is the authored order, which is also the order of the ids.
            table.Records.OrderBy(r => r.Position, StringComparer.Ordinal).Select(r => r.Id)
                .ShouldBe(table.Records.OrderBy(r => r.Id).Select(r => r.Id));
            table.Records.Select(r => r.Position).ShouldAllBe(p => !string.IsNullOrEmpty(p));
            table.Records.Select(r => r.Position).Distinct().Count().ShouldBe(table.Records.Count);

            foreach (var record in table.Records)
            {
                // Throws when a stored payload does not match the table schema.
                var aligned = record.Data.ValidateAndBackfillPayload(fields);
                aligned.Count.ShouldBe(fields.Count);
            }
        }
    }

    [Fact]
    public async Task SeedAsyncWhenRunAgainShouldChangeNothing()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var before = await TakeSnapshotAsync(ct);

        await fixture.RunDemoSeederAsync(ct);
        await fixture.RunDemoSeederAsync(ct);

        var after = await TakeSnapshotAsync(ct);

        after.ShouldBe(before);
        before.Users.ShouldBeGreaterThan(fixture.Accounts.Count);
        before.Records.ShouldBe(ExpectedDonorRecords + ExpectedVolunteerRecords + TopicRecordCount());
    }

    [Fact]
    public async Task SeederShouldResolveFromTheRealServiceProviderWithoutHttpContextOrDatabase()
    {
        // No connection is opened: this only proves that the container the application builds can construct
        // the seeder outside a request.
        await using var app = DemoDatabaseFixture.BuildApplication("Host=localhost;Database=unused");
        await using var scope = app.Services.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().ShouldNotBeNull();
    }

    [Fact]
    public void DefaultsShouldBeValidPublicDemoAccountsThatSatisfyThePasswordPolicy()
    {
        var accounts = DemoAccountOptions.Defaults;

        accounts.Select(a => a.Role).ShouldBe([RoleConstants.Admin, DemoAccountOptions.CoordinatorRole, DemoAccountOptions.VolunteerRole]);
        accounts.Select(a => a.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(accounts.Count);

        foreach (var account in accounts)
        {
            Validator.TryValidateObject(account, new ValidationContext(account), null, validateAllProperties: true).ShouldBeTrue();

            account.Email.ShouldEndWith(DemoEmailSuffix);
            account.Password.Length.ShouldBeGreaterThanOrEqualTo(8);
            account.Password.Any(char.IsAsciiDigit).ShouldBeTrue();
            account.Password.Any(char.IsAsciiLetterUpper).ShouldBeTrue();
            account.Password.Any(char.IsAsciiLetterLower).ShouldBeTrue();
            account.Password.Any(c => !char.IsAsciiLetterOrDigit(c)).ShouldBeTrue();
        }
    }

    [Fact]
    public void DemoContentShouldMatchTheTableSchemasAndStayClearlyFictional()
    {
        var now = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

        List<CustomField> donorFields =
        [
            .. new DonorBaseSystemTableTemplate().GetTemplate().Fields
                .OrderBy(f => f.Order)
                .Select(f => new CustomField { Name = f.Name, IsRequired = f.IsRequired, Order = f.Order }),
        ];
        var volunteerFields = DemoDataContent.CreateVolunteerFields();
        volunteerFields.EnsureValidFieldSchema();

        var donorRows = DemoDataContent.CreateDonorRows();
        var volunteerRows = DemoDataContent.CreateVolunteerRows(now);

        donorRows.Count.ShouldBe(ExpectedDonorRecords);
        volunteerRows.Count.ShouldBe(ExpectedVolunteerRecords);

        foreach (var row in donorRows)
        {
            // Every value names a field of the template (throws otherwise) and is valid for it.
            DemoDataContent.ToFieldValues(donorFields, row).ValidateAndBackfillPayload(donorFields).Count.ShouldBe(donorFields.Count);
        }

        foreach (var row in volunteerRows)
        {
            DemoDataContent.ToFieldValues(volunteerFields, row).ValidateAndBackfillPayload(volunteerFields).Count.ShouldBe(volunteerFields.Count);
            FakePhoneRegex().IsMatch(row["Телефон"]!).ShouldBeTrue();
            row["Електронна пошта"]!.ShouldEndWith(DemoEmailSuffix);

            // Dates are written so they sort as text.
            DateOnly.TryParseExact(row["Дата приєднання"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _).ShouldBeTrue();
        }

        var userFields = DemoDataContent.CreateUserCustomFields();
        userFields.EnsureValidFieldSchema();

        DemoDataContent.Colleagues.Count.ShouldBeInRange(MinColleagues, MaxColleagues);
        foreach (var colleague in DemoDataContent.Colleagues)
        {
            var profile = DemoDataContent.CreateProfile(colleague, Guid.NewGuid(), now, userFields);

            DemoDataContent.Email(colleague.EmailLocalPart).ShouldEndWith(DemoEmailSuffix);
            FakePhoneRegex().IsMatch(profile.PhoneNumber!).ShouldBeTrue();
            profile.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
            profile.CustomFields.ValidateAndBackfillPayload(userFields).Count.ShouldBe(userFields.Count);
            userFields.ShouldAllBe(f => !string.IsNullOrWhiteSpace(profile.CustomFields[f.Id.ToString()]!.GetValue<string>()));
        }
    }

    [Fact]
    public void TopicTablesShouldBeValidAndClearlyFictional()
    {
        var now = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

        DemoTables.All.Count.ShouldBeGreaterThanOrEqualTo(MinTopicTables);
        DemoTables.All.Select(t => t.Name)
            .Concat([DemoDataContent.DonorTemplateName, DemoDataContent.VolunteerTableName])
            .ShouldBeUnique();

        // Fake numbers must not repeat: a phone of the demo data belongs to one fictional person or organisation.
        List<string> phones = [.. DemoDataContent.CreateVolunteerRows(now).Select(r => r["Телефон"]!)];

        foreach (var definition in DemoTables.All)
        {
            definition.Name.Length.ShouldBeLessThanOrEqualTo(CustomTableValidationConstants.MaxNameLength);
            definition.Description.ShouldNotBeNullOrWhiteSpace();
            definition.Description.Length.ShouldBeLessThanOrEqualTo(CustomTableValidationConstants.MaxDescriptionLength);

            var fields = definition.CreateFields();
            fields.EnsureValidFieldSchema();
            fields.Count.ShouldBeLessThanOrEqualTo(CustomRecordValidationConstants.MaxFields);
            fields.Select(f => f.Name).ShouldBeUnique();

            var rows = definition.CreateRows(now);
            rows.Count.ShouldBeInRange(MinTopicTableRows, MaxTopicTableRows);

            foreach (var row in rows)
            {
                // Every value names a field of the schema (throws otherwise) and is valid for it.
                DemoDataContent.ToFieldValues(fields, row).ValidateAndBackfillPayload(fields).Count.ShouldBe(fields.Count);

                foreach (var column in definition.Columns)
                {
                    string? value = row[column.Name];

                    if (value is null)
                    {
                        column.IsRequired.ShouldBeFalse($"'{definition.Name}' / '{column.Name}' is required.");
                        continue;
                    }

                    value.ShouldNotBeNullOrWhiteSpace();
                    value.ShouldBe(value.Trim());

                    if (value.Contains('@', StringComparison.Ordinal))
                    {
                        value.ShouldEndWith(DemoEmailSuffix);
                    }

                    switch (column.Kind)
                    {
                        case DemoTables.ValueKind.Date:
                            DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                                .ShouldBeTrue($"'{definition.Name}' / '{column.Name}': {value}");
                            break;
                        case DemoTables.ValueKind.Number:
                            DemoNumberRegex().IsMatch(value).ShouldBeTrue($"'{definition.Name}' / '{column.Name}': {value}");
                            break;
                        case DemoTables.ValueKind.Phone:
                            FakePhoneRegex().IsMatch(value).ShouldBeTrue($"'{definition.Name}' / '{column.Name}': {value}");
                            phones.Add(value);
                            break;
                        case DemoTables.ValueKind.Email:
                            value.ShouldEndWith(DemoEmailSuffix);
                            break;
                        case DemoTables.ValueKind.YesNo:
                            value.ShouldBeOneOf("Так", "Ні");
                            break;
                        case DemoTables.ValueKind.Text:
                        default:
                            break;
                    }
                }
            }
        }

        phones.ShouldBeUnique();
    }

    private static async Task<HashSet<string>> GetRolePermissionsAsync(
        IServiceProvider services,
        string roleName,
        CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var roleId = await dbContext.Roles
            .AsNoTracking()
            .Where(r => r.Name == roleName)
            .Select(r => r.Id)
            .SingleAsync(cancellationToken);

        return await services.GetRequiredService<IRoleIdentityService>().GetRolePermissionsAsync(roleId, cancellationToken);
    }

    // E.164 numbers in the non-existent +380 00 000 00 xx range.
    [GeneratedRegex(@"^\+3800000000[0-9]{2}\z")]
    private static partial Regex FakePhoneRegex();

    // Digits, optionally with a decimal comma (as written in Ukrainian).
    [GeneratedRegex(@"^[0-9]+(,[0-9]+)?\z")]
    private static partial Regex DemoNumberRegex();

    private static int TopicRecordCount() => DemoTables.All.Sum(t => t.CreateRows(DateTimeOffset.UtcNow).Count);

    private async Task<DatabaseSnapshot> TakeSnapshotAsync(CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var securityStamps = await dbContext.Users
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Select(u => u.SecurityStamp + ":" + u.PasswordHash)
            .ToListAsync(cancellationToken);

        return new DatabaseSnapshot(
            Ngos: await dbContext.Set<Ngo>().CountAsync(cancellationToken),
            Users: await dbContext.Set<User>().CountAsync(cancellationToken),
            Roles: await dbContext.Roles.CountAsync(cancellationToken),
            RoleClaims: await dbContext.RoleClaims.CountAsync(cancellationToken),
            UserRoles: await dbContext.UserRoles.CountAsync(cancellationToken),
            UserCustomFields: await dbContext.Set<UserCustomField>().CountAsync(cancellationToken),
            Tables: await dbContext.Set<CustomTable>().CountAsync(cancellationToken),
            Fields: await dbContext.Set<CustomField>().CountAsync(cancellationToken),
            Records: await dbContext.Set<CustomRecord>().CountAsync(cancellationToken),
            Credentials: string.Join('|', securityStamps));
    }

    /// <summary>
    /// Starts PostgreSQL, builds the real service provider and performs the startup sequence once for all tests:
    /// migrations, the regular seeders and the first <see cref="DemoDataSeeder"/> run.
    /// </summary>
    public sealed class DemoDatabaseFixture : IAsyncLifetime
    {
        private PostgreSqlContainer? _container;
        private WebApplication? _app;

        public bool IsDockerAvailable { get; private set; }

        public IReadOnlyList<DemoAccountOptions> Accounts { get; } = DemoAccountOptions.Defaults;

        public IServiceProvider Services =>
            _app?.Services ?? throw new InvalidOperationException("The service provider is available only when Docker is running.");

        public async ValueTask InitializeAsync()
        {
            var ct = TestContext.Current.CancellationToken;

            try
            {
                _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
                await _container.StartAsync(ct);
            }
            catch (Exception)
            {
                // Only a missing container runtime skips the tests; any failure below must fail them.
                IsDockerAvailable = false;
                return;
            }

            IsDockerAvailable = true;

            _app = BuildApplication(_container.GetConnectionString());

            await _app.Services.UseInfrastructureDatabaseAsync(ct);
            await _app.Services.SeedDatabaseAsync(ct);
            await RunDemoSeederAsync(ct);
        }

        public async Task RunDemoSeederAsync(CancellationToken cancellationToken)
        {
            // A fresh scope per run, as at application startup.
            await using var scope = Services.CreateAsyncScope();
            var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();

            await seeder.SeedAsync(Accounts, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (_app is not null)
            {
                await _app.DisposeAsync();
            }

            if (_container is not null)
            {
                await _container.DisposeAsync();
            }
        }

        internal static WebApplication BuildApplication(string connectionString)
        {
            // Development turns on scope validation and ValidateOnBuild, so a seeder dependency
            // that the real container cannot provide fails here.
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development,
            });

            builder.Logging.SetMinimumLevel(LogLevel.Warning);

            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSqlConnection"] = connectionString,
                ["JwtOptions:Secret"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                ["JwtOptions:Issuer"] = "CrmApi",
                ["JwtOptions:Audience"] = "CrmClient",
                ["IdentityOptions:RequiredLength"] = "8",
                ["IdentityOptions:RequireDigit"] = "true",
                ["IdentityOptions:RequireNonAlphanumeric"] = "true",
                ["IdentityOptions:RequireUppercase"] = "true",
                ["IdentityOptions:RequireLowercase"] = "true",
                ["IdentityOptions:RequireUniqueEmail"] = "true",
                ["FileStorage:UploadDirectory"] = "uploads",
                ["FileStorage:BaseUrl"] = "http://localhost:5065",
                ["CorsSettings:AllowedOrigins:0"] = "http://localhost:4200",
            });

            // The same registrations as HostingExtensions.ConfigureServices, minus the HTTP pipeline and demo mode wiring.
            builder.Services.AddInfrastructureServices(builder.Configuration);
            builder.Services.AddApplicationServices();
            builder.Services.AddSingleton<IAppVersionProvider, AssemblyAppVersionProvider>();
            builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            builder.Services.AddScoped<DemoDataSeeder>();

            return builder.Build();
        }
    }

    private sealed record DatabaseSnapshot(
        int Ngos,
        int Users,
        int Roles,
        int RoleClaims,
        int UserRoles,
        int UserCustomFields,
        int Tables,
        int Fields,
        int Records,
        string Credentials);
}
