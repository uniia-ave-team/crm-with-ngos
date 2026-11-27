using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Crm.Application.Dtos.TableTemplates;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.DemoMode.DemoData;

/// <summary>
/// Fills an empty, already migrated database with fictional demo content: the organisation, roles,
/// the public demo accounts, a handful of colleagues and custom tables with records.
/// </summary>
/// <remarks>
/// <para>
/// The seeder is meant to run once at application startup, after the migrations and the regular database seeders
/// (the admin role and the system table templates must already exist). It needs no HTTP context and no current user.
/// </para>
/// <para>
/// It is idempotent: when the database already contains at least one user nothing is changed.
/// All writes happen in a single transaction, so a failed run leaves the database untouched.
/// </para>
/// <para>
/// Mediator commands are deliberately not used: each of them opens and commits its own transaction and the first
/// user/role flows expect an HTTP caller. The seeder calls the same building blocks the command handlers use instead
/// (<see cref="IIdentityService"/>, <see cref="IRoleIdentityService"/>, the repositories and the domain validation
/// helpers), so password policy, unique e-mails, field schema rules and record payload validation stay enforced.
/// </para>
/// </remarks>
public sealed partial class DemoDataSeeder(
    ApplicationDbContext dbContext,
    IUserRepository userRepository,
    INgoRepository ngoRepository,
    IAuthRoleRepository roleRepository,
    IRoleIdentityService roleIdentityService,
    IIdentityService identityService,
    IUserCustomFieldRepository userCustomFieldRepository,
    ITableTemplateRepository tableTemplateRepository,
    ICustomTableRepository customTableRepository,
    ICustomRecordRepository customRecordRepository,
    TimeProvider timeProvider,
    ILogger<DemoDataSeeder> logger)
{
    private const int EventIdBase = 99100;

    // Long enough to satisfy any configurable RequiredLength (the options allow up to 100 characters).
    private const int UnusablePasswordHexLength = 128;

    /// <summary>
    /// Seeds the demo content unless the database already contains users.
    /// </summary>
    /// <param name="accounts">The public demo accounts to create. Each one becomes an active user with the given role.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous seeding operation.</returns>
    /// <exception cref="ArgumentException">Thrown when no accounts are given, an account is invalid or an e-mail is repeated.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the regular seeders have not created the admin role or the system table template.</exception>
    public async Task SeedAsync(IReadOnlyList<DemoAccountOptions> accounts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        EnsureValidAccounts(accounts);

        if (await userRepository.AnyAsync(cancellationToken))
        {
            LogDemoDataAlreadySeeded(logger);
            return;
        }

        LogSeedingDemoData(logger, accounts.Count);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(ct => SeedInTransactionAsync(accounts, ct), cancellationToken);

        LogDemoDataSeeded(logger);
    }

    private static void EnsureValidAccounts(IReadOnlyList<DemoAccountOptions> accounts)
    {
        if (accounts.Count == 0)
        {
            throw new ArgumentException("At least one demo account is required.", nameof(accounts));
        }

        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < accounts.Count; i++)
        {
            var account = accounts[i]
                ?? throw new ArgumentException($"Demo account #{i + 1} is null.", nameof(accounts));

            var errors = new List<ValidationResult>();
            if (!Validator.TryValidateObject(account, new ValidationContext(account), errors, validateAllProperties: true))
            {
                string details = string.Join(" ", errors.Select(e => e.ErrorMessage));
                throw new ArgumentException($"Demo account #{i + 1} is invalid. {details}", nameof(accounts));
            }

            if (!emails.Add(account.Email))
            {
                throw new ArgumentException($"Demo account #{i + 1} repeats the e-mail of another demo account.", nameof(accounts));
            }
        }
    }

    // The fictional colleagues are never meant to be signed in to, so nobody learns their passwords.
    private static string CreateUnusablePassword() =>
        $"{RandomNumberGenerator.GetHexString(UnusablePasswordHexLength, lowercase: true)}-Aa1!";

    private async Task SeedInTransactionAsync(IReadOnlyList<DemoAccountOptions> accounts, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var now = timeProvider.GetUtcNow();

            var ngoId = await EnsureNgoAsync(now, cancellationToken);
            await EnsureRolesAsync(accounts, cancellationToken);
            var userFields = await EnsureUserCustomFieldsAsync(cancellationToken);
            await CreateUsersAsync(accounts, ngoId, now, userFields, cancellationToken);
            await CreateDonorTableAsync(ngoId, now, cancellationToken);
            await CreateVolunteerTableAsync(ngoId, now, cancellationToken);
            await CreateTopicTablesAsync(ngoId, now, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // The transaction is rolled back on dispose; drop the half-built entities so they cannot be saved later.
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<Guid> EnsureNgoAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await ngoRepository.AnyAsync(cancellationToken))
        {
            return await ngoRepository.GetIdAsync(cancellationToken);
        }

        var ngo = await ngoRepository.CreateAsync(
            new Ngo
            {
                Name = DemoDataContent.NgoName,
                CreatedAt = now.UtcDateTime.AddDays(-DemoDataContent.NgoAgeDays),
            },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        LogDemoNgoCreated(logger, ngo.Id);

        return ngo.Id;
    }

    private async Task EnsureRolesAsync(IReadOnlyList<DemoAccountOptions> accounts, CancellationToken cancellationToken)
    {
        foreach (string roleName in accounts.Select(a => a.Role).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await RoleExistsAsync(roleName, cancellationToken))
            {
                LogDemoRoleAlreadyExists(logger, roleName);
                continue;
            }

            if (string.Equals(roleName, RoleConstants.Admin, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Role '{RoleConstants.Admin}' does not exist. Run the regular database seeders before seeding demo data.");
            }

            var blueprint = DemoDataContent.GetRoleBlueprint(roleName);

            var roleId = await roleIdentityService.CreateRoleAsync(
                roleName,
                blueprint.FeminitiveName,
                blueprint.PluralName,
                cancellationToken);

            foreach (var right in blueprint.Rights)
            {
                await roleIdentityService.AddPermissionToRoleAsync(roleId, right.ToClaimValue(), cancellationToken);
            }

            LogDemoRoleCreated(logger, roleName, roleId, blueprint.Rights.Count);
        }
    }

    private async Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken)
    {
        try
        {
            await roleRepository.EnsureExistsAsync(roleName, cancellationToken);
            return true;
        }
        catch (NotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates the shared schema of user custom fields, unless the database already has one: then the demo profiles
    /// fill in only those of its fields that have the demo names.
    /// </summary>
    private async Task<IReadOnlyList<UserCustomField>> EnsureUserCustomFieldsAsync(CancellationToken cancellationToken)
    {
        var existing = await userCustomFieldRepository.GetAllOrderedAsync(cancellationToken);

        if (existing.Count > 0)
        {
            LogDemoUserCustomFieldsAlreadyExist(logger, existing.Count);
            return existing;
        }

        var fields = DemoDataContent.CreateUserCustomFields();
        fields.EnsureValidFieldSchema();

        await userCustomFieldRepository.ApplySchemaChangesAsync(addedFields: fields, removedFields: [], cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogDemoUserCustomFieldsCreated(logger, fields.Count);

        return fields;
    }

    private async Task CreateUsersAsync(
        IReadOnlyList<DemoAccountOptions> accounts,
        Guid ngoId,
        DateTimeOffset now,
        IReadOnlyList<UserCustomField> userFields,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < accounts.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var account = accounts[i];
            var profile = DemoDataContent.CreateAccountProfile(i, account.Name, ngoId, now, userFields);

            var user = await identityService.CreateUserAsync(account.Email, account.Password, profile, cancellationToken);
            await identityService.AddToRoleAsync(user.Id, account.Role, cancellationToken);
        }

        var accountEmails = accounts.Select(a => a.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var colleagueRoles = accounts
            .Select(a => a.Role)
            .Where(role => !string.Equals(role, RoleConstants.Admin, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        int colleagueCount = 0;

        for (int i = 0; i < DemoDataContent.Colleagues.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var colleague = DemoDataContent.Colleagues[i];
            string email = DemoDataContent.Email(colleague.EmailLocalPart);

            if (accountEmails.Contains(email))
            {
                continue;
            }

            var profile = DemoDataContent.CreateProfile(colleague, ngoId, now, userFields);
            var user = await identityService.CreateUserAsync(email, CreateUnusablePassword(), profile, cancellationToken);

            if (colleagueRoles.Count > 0)
            {
                await identityService.AddToRoleAsync(user.Id, colleagueRoles[i % colleagueRoles.Count], cancellationToken);
            }

            colleagueCount++;
        }

        LogDemoUsersCreated(logger, accounts.Count, colleagueCount);
    }

    private async Task CreateDonorTableAsync(Guid ngoId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var systemPresets = await tableTemplateRepository.GetPagedAsync<TableTemplateDto>(
            onlySystemPresets: true,
            pageSize: PaginationConstants.MaxPageSize,
            cancellationToken: cancellationToken);

        var template = systemPresets.Items.FirstOrDefault(
            t => string.Equals(t.Name, DemoDataContent.DonorTemplateName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"System table template '{DemoDataContent.DonorTemplateName}' does not exist. Run the regular database seeders before seeding demo data.");

        // Same instantiation as CreateCustomTableFromTemplateCommandHandler: the template's name, description and fields.
        var table = new CustomTable
        {
            NgoId = ngoId,
            Name = template.Name,
            Description = template.Description,
            SourceTemplateId = template.Id,
            Fields =
            [
                .. template.Fields.Select(f => new CustomField
                {
                    Name = f.Name,
                    IsRequired = f.IsRequired,
                    Order = f.Order,
                }),
            ],
        };

        await CreateTableWithRecordsAsync(table, DemoDataContent.CreateDonorRows(), now, cancellationToken);
    }

    private Task CreateVolunteerTableAsync(Guid ngoId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var table = new CustomTable
        {
            Name = DemoDataContent.VolunteerTableName,
            Description = DemoDataContent.VolunteerTableDescription,
            NgoId = ngoId,
            Fields = DemoDataContent.CreateVolunteerFields(),
        };

        return CreateTableWithRecordsAsync(table, DemoDataContent.CreateVolunteerRows(now), now, cancellationToken);
    }

    private async Task CreateTopicTablesAsync(Guid ngoId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var definition in DemoTables.All)
        {
            var table = new CustomTable
            {
                Name = definition.Name,
                Description = definition.Description,
                NgoId = ngoId,
                Fields = definition.CreateFields(),
            };

            await CreateTableWithRecordsAsync(table, definition.CreateRows(now), now, cancellationToken);
        }
    }

    private async Task CreateTableWithRecordsAsync(
        CustomTable table,
        List<Dictionary<string, string?>> rows,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        table.Fields.EnsureValidFieldSchema();

        await customTableRepository.CreateAsync(table, cancellationToken);

        List<CustomField> fields = [.. table.Fields.OrderBy(f => f.Order)];

        // Rows keep the authored order: in the manual order (evenly spaced positions, as after a rebalance)
        // and when sorted by Id (explicit, increasing UUIDv7 values).
        var positions = FractionalIndex.GenerateEvenlySpaced(rows.Count);
        var firstRecordTime = now.AddMinutes(-rows.Count);

        var records = rows
            .Select((row, index) => new CustomRecord
            {
                Id = Guid.CreateVersion7(firstRecordTime.AddMinutes(index)),
                CustomTableId = table.Id,
                Position = positions[index],
                Data = DemoDataContent.ToFieldValues(fields, row).ValidateAndBackfillPayload(fields),
            })
            .ToList();

        await customRecordRepository.CreateAsync(records, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogDemoTableCreated(logger, table.Name, table.Id, fields.Count, records.Count);
    }

    [LoggerMessage(EventId = EventIdBase + 1, Level = LogLevel.Information, Message = "Demo data seeding skipped: the database already contains users.")]
    private static partial void LogDemoDataAlreadySeeded(ILogger logger);

    [LoggerMessage(EventId = EventIdBase + 2, Level = LogLevel.Information, Message = "Seeding demo data with {AccountCount} demo account(s).")]
    private static partial void LogSeedingDemoData(ILogger logger, int accountCount);

    [LoggerMessage(EventId = EventIdBase + 3, Level = LogLevel.Information, Message = "Demo organisation created with ID: {NgoId}")]
    private static partial void LogDemoNgoCreated(ILogger logger, Guid ngoId);

    [LoggerMessage(EventId = EventIdBase + 4, Level = LogLevel.Information, Message = "Demo role '{RoleName}' already exists and is left unchanged.")]
    private static partial void LogDemoRoleAlreadyExists(ILogger logger, string roleName);

    [LoggerMessage(EventId = EventIdBase + 5, Level = LogLevel.Information, Message = "Demo role '{RoleName}' created with ID: {RoleId} and {PermissionCount} permission(s).")]
    private static partial void LogDemoRoleCreated(ILogger logger, string roleName, Guid roleId, int permissionCount);

    [LoggerMessage(EventId = EventIdBase + 6, Level = LogLevel.Information, Message = "Created {AccountCount} demo account user(s) and {ColleagueCount} fictional colleague(s).")]
    private static partial void LogDemoUsersCreated(ILogger logger, int accountCount, int colleagueCount);

    [LoggerMessage(EventId = EventIdBase + 7, Level = LogLevel.Information, Message = "Demo table '{TableName}' created with ID: {TableId} containing {FieldCount} field(s) and {RecordCount} record(s).")]
    private static partial void LogDemoTableCreated(ILogger logger, string tableName, Guid tableId, int fieldCount, int recordCount);

    [LoggerMessage(EventId = EventIdBase + 8, Level = LogLevel.Information, Message = "Demo data seeded successfully.")]
    private static partial void LogDemoDataSeeded(ILogger logger);

    [LoggerMessage(EventId = EventIdBase + 9, Level = LogLevel.Information, Message = "The user custom fields schema already has {FieldCount} field(s) and is left unchanged.")]
    private static partial void LogDemoUserCustomFieldsAlreadyExist(ILogger logger, int fieldCount);

    [LoggerMessage(EventId = EventIdBase + 10, Level = LogLevel.Information, Message = "Demo user custom fields schema created with {FieldCount} field(s).")]
    private static partial void LogDemoUserCustomFieldsCreated(ILogger logger, int fieldCount);
}
