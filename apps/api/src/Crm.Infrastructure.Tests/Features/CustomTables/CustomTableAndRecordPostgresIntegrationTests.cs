using System.Text.Json.Nodes;
using Crm.Application.Dtos.Common;
using Crm.Application.Dtos.CustomRecords;
using Crm.Application.Dtos.CustomTables;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.CustomRecords.Mappings;
using Crm.Application.Features.CustomRecords.Services;
using Crm.Application.Features.CustomTables.Commands;
using Crm.Application.Features.CustomTables.Mappings;
using Crm.Application.Features.TableTemplates.Commands;
using Crm.Application.Features.TableTemplates.Mappings;
using Crm.Application.Features.Users.Mappings;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.ValueObjects;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Interceptors;
using Crm.Infrastructure.Persistence.Seeders;
using Crm.Infrastructure.Persistence.Seeders.SystemPresets;
using Crm.Infrastructure.Repositories;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Crm.Infrastructure.Tests.Features.CustomTables;

/// <summary>
/// Executes real SQL integration tests against a PostgreSQL 17 Testcontainer instance
/// to verify immutable server-generated field keys, atomic <c>crm_sync_jsonb_keys</c> schema sync,
/// <see cref="TableTemplate"/> JSONB field storage, JSONB value-only prefix full-text search,
/// language-agnostic smart dynamic sorting via <c>crm_try_*</c> functions, and fractional-index
/// <see cref="CustomRecord.Position"/> ordering, drag-and-drop moves, deletion, rebalancing, concurrency, and cascade delete.
/// </summary>
public class CustomTableAndRecordPostgresIntegrationTests(
    CustomTableAndRecordPostgresIntegrationTests.PostgresFixture fixture)
    : IClassFixture<CustomTableAndRecordPostgresIntegrationTests.PostgresFixture>
{
    static CustomTableAndRecordPostgresIntegrationTests()
    {
        new CustomTableMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
        new TableTemplateMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
        new CustomRecordMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
        new UserMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public async Task UpdateCustomTableShouldPreserveImmutableKeysDropRemovedKeysAndBackfillRequiredValuesInPostgres()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var setupContext = fixture.CreateDbContext();
        var ngo = new Ngo { Id = Guid.CreateVersion7(), Name = $"NGO-{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        var cityFieldId = Guid.CreateVersion7();
        var obsoleteFieldId = Guid.CreateVersion7();
        string cityKey = cityFieldId.ToString();
        string obsoleteKey = obsoleteFieldId.ToString();

        var table = new CustomTable
        {
            Id = Guid.CreateVersion7(),
            NgoId = ngo.Id,
            Name = "Beneficiaries",
            Fields =
            [
                new CustomField
                {
                    Id = cityFieldId,
                    Name = "City",
                    IsRequired = false,
                    Order = 0,
                },
                new CustomField
                {
                    Id = obsoleteFieldId,
                    Name = "Legacy Note",
                    IsRequired = false,
                    Order = 1,
                },
            ],
        };

        var recordWithCity = new CustomRecord
        {
            Id = Guid.CreateVersion7(),
            CustomTableId = table.Id,
            Data = new JsonObject { [cityKey] = "Kyiv", [obsoleteKey] = "drop-me" },
        };
        var recordWithoutCity = new CustomRecord
        {
            Id = Guid.CreateVersion7(),
            CustomTableId = table.Id,
            Data = new JsonObject { [obsoleteKey] = "drop-me-too" },
        };
        AssignPositions(recordWithCity, recordWithoutCity);

        setupContext.AddRange(ngo, table, recordWithCity, recordWithoutCity);
        await setupContext.SaveChangesAsync(ct);

        // Act
        await using var actContext = fixture.CreateDbContext();
        await using var unitOfWork = new UnitOfWork(actContext);
        var tableRepo = new CustomTableRepository(actContext);
        var recordRepo = new CustomRecordRepository(actContext);
        var handler = new UpdateCustomTableCommandHandler(tableRepo, recordRepo, NullLogger<UpdateCustomTableCommandHandler>.Instance);

        var command = new UpdateCustomTableCommand(
            "Beneficiaries Updated",
            "Updated description",
            [
                new UpdateCustomFieldDto(cityFieldId, "City (Mandatory)", IsRequired: true, Order: 0),
                new UpdateCustomFieldDto(null, "Notes", IsRequired: false, Order: 1),
                new UpdateCustomFieldDto(null, "Region", IsRequired: true, Order: 2),
            ])
        {
            Id = table.Id,
        };

        await unitOfWork.BeginTransactionAsync(ct);
        await handler.Handle(command, ct);
        await unitOfWork.CommitTransactionAsync(ct);

        // Assert
        await using var assertContext = fixture.CreateDbContext();
        var persistedTable = await assertContext.Set<CustomTable>()
            .Include(t => t.Fields)
            .SingleAsync(t => t.Id == table.Id, ct);

        persistedTable.Name.ShouldBe("Beneficiaries Updated");
        persistedTable.Fields.Count.ShouldBe(3);

        var persistedCityField = persistedTable.Fields.Single(f => f.Id == cityFieldId);
        persistedCityField.Name.ShouldBe("City (Mandatory)");
        persistedCityField.IsRequired.ShouldBeTrue();

        var notesField = persistedTable.Fields.Single(f => f.Name == "Notes");
        var regionField = persistedTable.Fields.Single(f => f.Name == "Region");
        notesField.Id.ShouldNotBe(Guid.Empty);
        regionField.Id.ShouldNotBe(Guid.Empty);

        var records = await assertContext.Set<CustomRecord>()
            .Where(r => r.CustomTableId == table.Id)
            .ToDictionaryAsync(r => r.Id, ct);

        records[recordWithCity.Id].Data[cityKey]?.GetValue<string>().ShouldBe("Kyiv");
        records[recordWithoutCity.Id].Data[cityKey]?.GetValue<string>().ShouldBe(string.Empty);
        records[recordWithCity.Id].Data[regionField.Id.ToString()]?.GetValue<string>().ShouldBe(string.Empty);
        records[recordWithoutCity.Id].Data[regionField.Id.ToString()]?.GetValue<string>().ShouldBe(string.Empty);
        records[recordWithCity.Id].Data.ContainsKey(obsoleteKey).ShouldBeFalse();
        records[recordWithoutCity.Id].Data.ContainsKey(obsoleteKey).ShouldBeFalse();
        records[recordWithCity.Id].Data.ContainsKey(notesField.Id.ToString()).ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateTableTemplateShouldPersistOwnedFieldsJsonbInPostgres()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var setupContext = fixture.CreateDbContext();
        var template = new TableTemplate
        {
            Id = Guid.CreateVersion7(),
            Name = $"Template-{Guid.NewGuid():N}",
            IsSystemPreset = false,
            Fields =
            [
                new TemplateFieldDefinition("First", false, 0),
                new TemplateFieldDefinition("Second", true, 1),
            ],
        };

        setupContext.Add(template);
        await setupContext.SaveChangesAsync(ct);

        // Act
        await using (var actContext = fixture.CreateDbContext())
        {
            await using var uow = new UnitOfWork(actContext);
            var repo = new TableTemplateRepository(actContext);
            var handler = new UpdateTableTemplateCommandHandler(repo, NullLogger<UpdateTableTemplateCommandHandler>.Instance);

            var command = new UpdateTableTemplateCommand(
                template.Name,
                "Updated JSONB template",
                [
                    new FieldDefinitionDto("First Updated", IsRequired: true, Order: 0),
                    new FieldDefinitionDto("Third Added", IsRequired: false, Order: 1),
                ])
            {
                Id = template.Id,
            };

            await uow.BeginTransactionAsync(ct);
            await handler.Handle(command, ct);
            await uow.CommitTransactionAsync(ct);
        }

        // Assert
        await using var assertContext = fixture.CreateDbContext();
        var persisted = await assertContext.Set<TableTemplate>()
            .AsNoTracking()
            .SingleAsync(t => t.Id == template.Id, ct);

        persisted.Description.ShouldBe("Updated JSONB template");
        persisted.Fields.Count.ShouldBe(2);
        persisted.Fields[0].Name.ShouldBe("First Updated");
        persisted.Fields[0].IsRequired.ShouldBeTrue();
        persisted.Fields[1].Name.ShouldBe("Third Added");
        persisted.Fields[1].IsRequired.ShouldBeFalse();
    }

    [Fact]
    public async Task TableTemplateSeederShouldCreateAndSynchronizeChangedColumnsInPostgres()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        string presetName = $"SystemPreset-{Guid.NewGuid():N}";

        // 1. Seed initial preset (EF Core generates ID automatically)
        await using (var ctx1 = fixture.CreateDbContext())
        {
            await using var uow1 = new UnitOfWork(ctx1);
            var seeder1 = new TableTemplateSeeder(
                [new TestSystemPreset(presetName, "v1", [new("Col A", true, 0)])],
                uow1,
                new TableTemplateRepository(ctx1),
                NullLogger<TableTemplateSeeder>.Instance);

            await seeder1.SeedAsync(ct);
        }

        // 2. Re-seed with updated Description, renamed column, and new column
        await using (var ctx2 = fixture.CreateDbContext())
        {
            await using var uow2 = new UnitOfWork(ctx2);
            var seeder2 = new TableTemplateSeeder(
                [new TestSystemPreset(presetName, "v2", [new("Col A Updated", false, 0), new("Col B Added", true, 1)])],
                uow2,
                new TableTemplateRepository(ctx2),
                NullLogger<TableTemplateSeeder>.Instance);

            await seeder2.SeedAsync(ct);
        }

        // Assert persisted JSONB updated in place
        await using var assertCtx = fixture.CreateDbContext();
        var persisted = await assertCtx.Set<TableTemplate>()
            .AsNoTracking()
            .SingleAsync(t => t.Name == presetName, ct);

        persisted.Id.ShouldNotBe(Guid.Empty);
        persisted.Description.ShouldBe("v2");
        persisted.Fields.ShouldBe(
        [
            new TemplateFieldDefinition("Col A Updated", false, 0),
            new TemplateFieldDefinition("Col B Added", true, 1),
        ]);
    }

    [Fact]
    public async Task GetPagedAsyncSearchShouldMatchStringValuePrefixesAndIgnoreJsonKeyNamesInPostgres()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var context = fixture.CreateDbContext();
        var ngo = new Ngo { Id = Guid.CreateVersion7(), Name = $"NGO-{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        var table = new CustomTable
        {
            Id = Guid.CreateVersion7(),
            NgoId = ngo.Id,
            Name = "Donors",
        };

        var kyivRecord = new CustomRecord
        {
            Id = Guid.CreateVersion7(),
            CustomTableId = table.Id,
            Data = new JsonObject
            {
                ["f_city"] = "Kyiv",
                ["f_organization"] = "Charity Foundation",
                ["f_amount"] = "1500",
            },
        };
        var lvivRecord = new CustomRecord
        {
            Id = Guid.CreateVersion7(),
            CustomTableId = table.Id,
            Data = new JsonObject
            {
                ["f_city"] = "Lviv",
                ["f_organization"] = "Volunteer Network",
                ["f_amount"] = "450",
            },
        };
        AssignPositions(kyivRecord, lvivRecord);

        context.AddRange(ngo, table, kyivRecord, lvivRecord);
        await context.SaveChangesAsync(ct);

        var repository = new CustomRecordRepository(context);

        // Searching by JSON key names must NOT match records
        var byCityKey = await repository.GetPagedAsync<CustomRecordDto>(table.Id, searchTerm: "f_city", cancellationToken: ct);
        byCityKey.TotalCount.ShouldBe(0);

        var byAmountKey = await repository.GetPagedAsync<CustomRecordDto>(table.Id, searchTerm: "f_amount", cancellationToken: ct);
        byAmountKey.TotalCount.ShouldBe(0);

        // Prefix search ("Kyi") must match "Kyiv"
        var byPrefix = await repository.GetPagedAsync<CustomRecordDto>(table.Id, searchTerm: "Kyi", cancellationToken: ct);
        byPrefix.TotalCount.ShouldBe(1);
        byPrefix.Items.Single().Id.ShouldBe(kyivRecord.Id);

        // Multi-token prefix search ("Kyi Char") must match record containing both prefixes
        var byMultiPrefix = await repository.GetPagedAsync<CustomRecordDto>(table.Id, searchTerm: "Kyi Char", cancellationToken: ct);
        byMultiPrefix.TotalCount.ShouldBe(1);
        byMultiPrefix.Items.Single().Id.ShouldBe(kyivRecord.Id);

        // Numeric string prefix search ("150") must match "1500"
        var byNumericPrefix = await repository.GetPagedAsync<CustomRecordDto>(table.Id, searchTerm: "150", cancellationToken: ct);
        byNumericPrefix.TotalCount.ShouldBe(1);
        byNumericPrefix.Items.Single().Id.ShouldBe(kyivRecord.Id);
    }

    [Fact]
    public async Task GetPagedAsyncSmartOrderingShouldInferNumbersMixedDatesAndLanguageAgnosticTextInPostgres()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var context = fixture.CreateDbContext();
        var ngo = new Ngo { Id = Guid.CreateVersion7(), Name = $"NGO-{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        var table = new CustomTable
        {
            Id = Guid.CreateVersion7(),
            NgoId = ngo.Id,
            Name = "SmartOrderingTable",
        };

        string amountKey = Guid.CreateVersion7().ToString();
        string txDateKey = Guid.CreateVersion7().ToString();
        string labelKey = Guid.CreateVersion7().ToString();

        var rNeg = new CustomRecord
        {
            Id = Guid.CreateVersion7(),
            CustomTableId = table.Id,
            Data = new JsonObject { [amountKey] = "-10", [txDateKey] = "2026-01-15", [labelKey] = "Alpha" },
        };
        var rTwo = new CustomRecord
        {
            Id = Guid.CreateVersion7(),
            CustomTableId = table.Id,
            Data = new JsonObject { [amountKey] = "2", [txDateKey] = "2026-05-20", [labelKey] = "Bravo" },
        };
        var rTen = new CustomRecord
        {
            Id = Guid.CreateVersion7(),
            CustomTableId = table.Id,
            Data = new JsonObject { [amountKey] = "10", [txDateKey] = "2026-10-01", [labelKey] = "Charlie" },
        };
        var rTenHalf = new CustomRecord
        {
            Id = Guid.CreateVersion7(),
            CustomTableId = table.Id,
            Data = new JsonObject { [amountKey] = "10.5", [txDateKey] = "2026-11-01", [labelKey] = "Delta" },
        };
        var rHundred = new CustomRecord
        {
            Id = Guid.CreateVersion7(),
            CustomTableId = table.Id,
            Data = new JsonObject { [amountKey] = "100", [txDateKey] = "2026-12-01", [labelKey] = "Echo" },
        };
        var rNote = new CustomRecord
        {
            Id = Guid.CreateVersion7(),
            CustomTableId = table.Id,
            Data = new JsonObject { [amountKey] = "pending", [txDateKey] = null, [labelKey] = "Foxtrot" },
        };
        var rNull = new CustomRecord
        {
            Id = Guid.CreateVersion7(),
            CustomTableId = table.Id,
            Data = new JsonObject { [amountKey] = null, [txDateKey] = null, [labelKey] = null },
        };
        AssignPositions(rTen, rNull, rHundred, rNote, rNeg, rTenHalf, rTwo);

        context.AddRange(ngo, table, rTen, rNull, rHundred, rNote, rNeg, rTenHalf, rTwo);
        await context.SaveChangesAsync(ct);

        var repository = new CustomRecordRepository(context);

        // 1. Mixed numeric column (5/6 = 83% numbers >= 80%): valid numbers sorted numerically, text outlier after numbers, null last.
        var ascNumbers = await repository.GetPagedAsync<CustomRecordDto>(
            table.Id,
            orderBy: amountKey,
            sortOrder: SortOrderConstants.Ascending,
            cancellationToken: ct);
        ascNumbers.Items.Select(x => x.Id).ShouldBe([rNeg.Id, rTwo.Id, rTen.Id, rTenHalf.Id, rHundred.Id, rNote.Id, rNull.Id]);

        var descNumbers = await repository.GetPagedAsync<CustomRecordDto>(
            table.Id,
            orderBy: amountKey,
            sortOrder: SortOrderConstants.Descending,
            cancellationToken: ct);
        descNumbers.Items.Select(x => x.Id).ShouldBe([rHundred.Id, rTenHalf.Id, rTen.Id, rTwo.Id, rNeg.Id, rNote.Id, rNull.Id]);

        // 2. ISO date strings sorted chronologically with nulls last in both directions.
        var ascDates = await repository.GetPagedAsync<CustomRecordDto>(
            table.Id,
            orderBy: txDateKey,
            sortOrder: SortOrderConstants.Ascending,
            cancellationToken: ct);
        ascDates.Items.Take(5).Select(x => x.Id).ShouldBe([rNeg.Id, rTwo.Id, rTen.Id, rTenHalf.Id, rHundred.Id]);
        ascDates.Items.Skip(5).Select(x => x.Id).ShouldBe([rNote.Id, rNull.Id], ignoreOrder: true);

        var descDates = await repository.GetPagedAsync<CustomRecordDto>(
            table.Id,
            orderBy: txDateKey,
            sortOrder: SortOrderConstants.Descending,
            cancellationToken: ct);
        descDates.Items.Take(5).Select(x => x.Id).ShouldBe([rHundred.Id, rTenHalf.Id, rTen.Id, rTwo.Id, rNeg.Id]);

        // 3. Language-agnostic text ordering with nulls last in both ASC and DESC.
        var ascText = await repository.GetPagedAsync<CustomRecordDto>(
            table.Id,
            orderBy: labelKey,
            sortOrder: SortOrderConstants.Ascending,
            cancellationToken: ct);
        ascText.Items.Select(x => x.Id).ShouldBe([rNeg.Id, rTwo.Id, rTen.Id, rTenHalf.Id, rHundred.Id, rNote.Id, rNull.Id]);

        var descText = await repository.GetPagedAsync<CustomRecordDto>(
            table.Id,
            orderBy: labelKey,
            sortOrder: SortOrderConstants.Descending,
            cancellationToken: ct);
        descText.Items.Select(x => x.Id).ShouldBe([rNote.Id, rHundred.Id, rTenHalf.Id, rTen.Id, rTwo.Id, rNeg.Id, rNull.Id]);
    }

    [Fact]
    public async Task AuthUserRepositorySmartOrderingShouldSortUserCustomFieldsNumericallyAndByTextInPostgres()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var context = fixture.CreateDbContext();
        var tag = Guid.NewGuid().ToString("N");

        var user1 = CreateAuthUser($"u1-{tag}@ngo.org", new JsonObject { ["f_exp"] = "2", ["f_tag"] = $"{tag} Alpha" });
        var user2 = CreateAuthUser($"u2-{tag}@ngo.org", new JsonObject { ["f_exp"] = "10", ["f_tag"] = $"{tag} Beta" });
        var user3 = CreateAuthUser($"u3-{tag}@ngo.org", new JsonObject { ["f_exp"] = null, ["f_tag"] = null });

        context.AddRange(user1, user2, user3);
        await context.SaveChangesAsync(ct);

        var repo = new AuthUserRepository(context);

        var ascByNumber = await repo.GetPagedAsync<UserDto>(
            searchTerm: tag,
            customFieldSortKey: "f_exp",
            sortOrder: SortOrderConstants.Ascending,
            searchablePublicCustomFieldKeys: ["f_tag"],
            cancellationToken: ct);
        ascByNumber.Items.Select(u => u.Id).ShouldBe([user1.Id, user2.Id, user3.Id]);

        var descByNumber = await repo.GetPagedAsync<UserDto>(
            searchTerm: tag,
            customFieldSortKey: "f_exp",
            sortOrder: SortOrderConstants.Descending,
            searchablePublicCustomFieldKeys: ["f_tag"],
            cancellationToken: ct);
        descByNumber.Items.Select(u => u.Id).ShouldBe([user2.Id, user1.Id, user3.Id]);

        var ascText = await repo.GetPagedAsync<UserDto>(
            searchTerm: tag,
            customFieldSortKey: "f_tag",
            sortOrder: SortOrderConstants.Ascending,
            searchablePublicCustomFieldKeys: ["f_tag"],
            cancellationToken: ct);
        ascText.Items.Select(u => u.Id).ShouldBe([user1.Id, user2.Id, user3.Id]);
    }

    [Fact]
    public async Task CustomRecordFractionalPositioningShouldSupportInsertMoveDeletePaginationSearchRebalanceAndPreserveMetadataInPostgres()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        Guid tableId;
        Guid otherTableId;
        Guid otherTableRecordId;
        var initialTimestamp = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        await using (var setupContext = fixture.CreateDbContext())
        {
            var ngo = new Ngo { Id = Guid.CreateVersion7(), Name = $"NGO-{Guid.NewGuid():N}", CreatedAt = initialTimestamp };
            var table = new CustomTable { Id = Guid.CreateVersion7(), NgoId = ngo.Id, Name = "RankedRows" };
            var otherTable = new CustomTable { Id = Guid.CreateVersion7(), NgoId = ngo.Id, Name = "OtherTable" };
            var otherRecord = new CustomRecord
            {
                Id = Guid.CreateVersion7(),
                CustomTableId = otherTable.Id,
                Position = FractionalIndex.Initial,
                Data = new JsonObject { ["f_title"] = "Foreign" },
            };

            tableId = table.Id;
            otherTableId = otherTable.Id;
            otherTableRecordId = otherRecord.Id;

            setupContext.AddRange(ngo, table, otherTable, otherRecord);
            await setupContext.SaveChangesAsync(ct);
        }

        async Task<Guid> InsertRecordAsync(string title, Guid? prevId = null, Guid? nextId = null)
        {
            await using var ctx = fixture.CreateDbContext();
            await using var uow = new UnitOfWork(ctx);
            var tableRepo = new CustomTableRepository(ctx);
            var recordRepo = new CustomRecordRepository(ctx);
            var svc = new CustomRecordPositionService(tableRepo, recordRepo);

            await uow.BeginTransactionAsync(ct);
            await tableRepo.LockForUpdateAsync(tableId, ct);
            string position = await svc.ComputeInsertPositionAsync(tableId, prevId, nextId, ct);
            var rec = new CustomRecord
            {
                Id = Guid.CreateVersion7(),
                CustomTableId = tableId,
                Position = position,
                Data = new JsonObject { ["f_title"] = title },
                CreatedAt = initialTimestamp,
                UpdatedAt = initialTimestamp,
            };

            await recordRepo.CreateAsync(rec, ct);
            await uow.CommitTransactionAsync(ct);
            return rec.Id;
        }

        async Task MoveRecordAsync(Guid id, Guid? prevId, Guid? nextId)
        {
            await using var ctx = fixture.CreateDbContext();
            await using var uow = new UnitOfWork(ctx);
            var svc = new CustomRecordPositionService(new CustomTableRepository(ctx), new CustomRecordRepository(ctx));

            await uow.BeginTransactionAsync(ct);
            await svc.MoveAsync(id, prevId, nextId, ct);
            await uow.CommitTransactionAsync(ct);
        }

        async Task DeleteRecordAsync(Guid id)
        {
            await using var ctx = fixture.CreateDbContext();
            var repo = new CustomRecordRepository(ctx);
            await repo.DeleteAsync(id, ct);
        }

        async Task<List<CustomRecordDto>> ReadAllOrderedAsync(string sortOrder = SortOrderConstants.Ascending)
        {
            await using var ctx = fixture.CreateDbContext();
            var repo = new CustomRecordRepository(ctx);
            var page = await repo.GetPagedAsync<CustomRecordDto>(tableId, orderBy: null, sortOrder: sortOrder, pageNumber: 1, pageSize: 50, cancellationToken: ct);
            return [.. page.Items];
        }

        // 1. Insert: empty table -> A; append tail (null, null) -> [A, B]; explicit tail (B, null) -> [A, B, C]
        var idA = await InsertRecordAsync("Alpha Match");
        var idB = await InsertRecordAsync("Beta");
        var idC = await InsertRecordAsync("Gamma Match", prevId: idB, nextId: null);

        // Prepend head (null, A) -> [H, A, B, C]; insert middle (A, B) -> [H, A, M, B, C]
        var idH = await InsertRecordAsync("Head", prevId: null, nextId: idA);
        var idM = await InsertRecordAsync("Middle Match", prevId: idA, nextId: idB);

        // Verify native B-tree Position ASC and DESC
        (await ReadAllOrderedAsync(SortOrderConstants.Ascending)).Select(x => x.Id).ShouldBe([idH, idA, idM, idB, idC]);
        (await ReadAllOrderedAsync(SortOrderConstants.Descending)).Select(x => x.Id).ShouldBe([idC, idB, idM, idA, idH]);

        // Verify pagination preserves manual order across pages (pageSize=2)
        await using (var readCtx = fixture.CreateDbContext())
        {
            var repo = new CustomRecordRepository(readCtx);
            var page1 = await repo.GetPagedAsync<CustomRecordDto>(tableId, orderBy: null, pageNumber: 1, pageSize: 2, cancellationToken: ct);
            var page2 = await repo.GetPagedAsync<CustomRecordDto>(tableId, orderBy: null, pageNumber: 2, pageSize: 2, cancellationToken: ct);
            var page3 = await repo.GetPagedAsync<CustomRecordDto>(tableId, orderBy: null, pageNumber: 3, pageSize: 2, cancellationToken: ct);

            page1.TotalCount.ShouldBe(5);
            page1.Items.Select(x => x.Id).ShouldBe([idH, idA]);
            page2.Items.Select(x => x.Id).ShouldBe([idM, idB]);
            page3.Items.Select(x => x.Id).ShouldBe([idC]);

            // SearchTerm preserves manual row order: [A, M, C]
            var searchMatch = await repo.GetPagedAsync<CustomRecordDto>(tableId, searchTerm: "Match", orderBy: null, cancellationToken: ct);
            searchMatch.TotalCount.ShouldBe(3);
            searchMatch.Items.Select(x => x.Id).ShouldBe([idA, idM, idC]);
        }

        // Verify 409 on stale neighbors (A and B are no longer adjacent) & 404 on foreign-table neighbor
        await Should.ThrowAsync<ConcurrencyConflictException>(() => InsertRecordAsync("Conflict", prevId: idA, nextId: idB));
        await Should.ThrowAsync<EntityNotFoundException<CustomRecord>>(() => InsertRecordAsync("Foreign", prevId: otherTableRecordId, nextId: null));

        // 2. Drag-and-Drop Moves on [H, A, M, B, C]:
        await MoveRecordAsync(idH, null, null);
        (await ReadAllOrderedAsync()).Select(x => x.Id).ShouldBe([idA, idM, idB, idC, idH]);

        await MoveRecordAsync(idH, null, idA);
        (await ReadAllOrderedAsync()).Select(x => x.Id).ShouldBe([idH, idA, idM, idB, idC]);

        await MoveRecordAsync(idA, idM, idB);
        (await ReadAllOrderedAsync()).Select(x => x.Id).ShouldBe([idH, idM, idA, idB, idC]);

        await MoveRecordAsync(idA, idH, idM);
        (await ReadAllOrderedAsync()).Select(x => x.Id).ShouldBe([idH, idA, idM, idB, idC]);

        await MoveRecordAsync(idC, idH, idA);
        (await ReadAllOrderedAsync()).Select(x => x.Id).ShouldBe([idH, idC, idA, idM, idB]);

        // No-op move (C already between H and A) -> unchanged
        await MoveRecordAsync(idC, idH, idA);
        (await ReadAllOrderedAsync()).Select(x => x.Id).ShouldBe([idH, idC, idA, idM, idB]);

        // 3. Set-based JSONB rebalance preserves exact order while compacting ranks without touching UpdatedAt
        await using (var rebCtx = fixture.CreateDbContext())
        {
            await using var uow = new UnitOfWork(rebCtx);
            var tableRepo = new CustomTableRepository(rebCtx);
            var recordRepo = new CustomRecordRepository(rebCtx);
            var svc = new CustomRecordPositionService(tableRepo, recordRepo);
            await uow.BeginTransactionAsync(ct);
            await tableRepo.LockForUpdateAsync(tableId, ct);
            await svc.RebalanceTableAsync(tableId, ct);
            await uow.CommitTransactionAsync(ct);
        }

        var afterRebalance = await ReadAllOrderedAsync();
        afterRebalance.Select(x => x.Id).ShouldBe([idH, idC, idA, idM, idB]);
        await using (var rebalancedCtx = fixture.CreateDbContext())
        {
            var compactedPositions = await rebalancedCtx.Set<CustomRecord>()
                .Where(r => r.CustomTableId == tableId)
                .Select(r => r.Position)
                .ToListAsync(ct);
            compactedPositions.Count.ShouldBe(5);
            compactedPositions.All(p => p.Length <= 3).ShouldBeTrue();
        }

        // 4. Direct deletion requires 0 neighbor updates and leaves neighbor UpdatedAt untouched!
        await DeleteRecordAsync(idA);
        (await ReadAllOrderedAsync()).Select(x => x.Id).ShouldBe([idH, idC, idM, idB]);

        await using (var checkCtx = fixture.CreateDbContext())
        {
            var neighborC = await checkCtx.Set<CustomRecord>().SingleAsync(r => r.Id == idC, ct);
            var neighborM = await checkCtx.Set<CustomRecord>().SingleAsync(r => r.Id == idM, ct);
            string.CompareOrdinal(neighborC.Position, neighborM.Position).ShouldBeLessThan(0);
            neighborC.UpdatedAt.ShouldBe(initialTimestamp);
            neighborM.UpdatedAt.ShouldBe(initialTimestamp);
        }

        await DeleteRecordAsync(idH);
        await DeleteRecordAsync(idB);
        (await ReadAllOrderedAsync()).Select(x => x.Id).ShouldBe([idC, idM]);

        await DeleteRecordAsync(idC);
        await DeleteRecordAsync(idM);
        (await ReadAllOrderedAsync()).ShouldBeEmpty();

        otherTableId.ShouldNotBe(tableId);
    }

    [Fact]
    public async Task ConcurrentAppendsAndCascadeHardDeleteShouldMaintainIntegrityInPostgres()
    {
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        Guid ngoId;
        Guid tableId;

        await using (var setupContext = fixture.CreateDbContext())
        {
            var ngo = new Ngo { Id = Guid.CreateVersion7(), Name = $"NGO-Conc-{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
            var table = new CustomTable { Id = Guid.CreateVersion7(), NgoId = ngo.Id, Name = "ConcurrentTable" };
            ngoId = ngo.Id;
            tableId = table.Id;

            setupContext.AddRange(ngo, table);
            await setupContext.SaveChangesAsync(ct);
        }

        // Launch 8 concurrent appends under pessimistic table lock.
        var tasks = Enumerable.Range(0, 8).Select(async i =>
        {
            await using var ctx = fixture.CreateDbContext();
            await using var uow = new UnitOfWork(ctx);
            var tableRepo = new CustomTableRepository(ctx);
            var recordRepo = new CustomRecordRepository(ctx);
            var svc = new CustomRecordPositionService(tableRepo, recordRepo);

            await uow.BeginTransactionAsync(ct);
            await tableRepo.LockForUpdateAsync(tableId, ct);
            string position = await svc.ComputeInsertPositionAsync(tableId, null, null, ct);
            var rec = new CustomRecord
            {
                Id = Guid.CreateVersion7(),
                CustomTableId = tableId,
                Position = position,
                Data = new JsonObject { ["f_seq"] = i.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            };

            await recordRepo.CreateAsync(rec, ct);
            await uow.CommitTransactionAsync(ct);
            return rec.Id;
        });

        Guid[] insertedIds = await Task.WhenAll(tasks);

        // Verify all 8 have strictly increasing unique positions!
        await using (var assertCtx = fixture.CreateDbContext())
        {
            var repo = new CustomRecordRepository(assertCtx);
            var ordered = await repo.GetPagedAsync<CustomRecordDto>(tableId, orderBy: null, pageNumber: 1, pageSize: 20, cancellationToken: ct);
            ordered.TotalCount.ShouldBe(8);
            ordered.Items.Select(x => x.Id).ShouldBe(insertedIds, ignoreOrder: true);

            var positions = await assertCtx.Set<CustomRecord>()
                .Where(r => r.CustomTableId == tableId)
                .OrderBy(r => r.Position)
                .Select(r => r.Position)
                .ToListAsync(ct);
            positions.Count.ShouldBe(8);
            for (int i = 1; i < positions.Count; i++)
            {
                string.CompareOrdinal(positions[i - 1], positions[i]).ShouldBeLessThan(0);
            }
        }

        // Verify DB check constraint rejects empty Position and COLLATE "C" orders "Zz" < "a0" < "b00".
        await using (var constraintCtx = fixture.CreateDbContext())
        {
            constraintCtx.Add(new CustomRecord
            {
                Id = Guid.CreateVersion7(),
                CustomTableId = tableId,
                Position = string.Empty,
                Data = [],
            });
            await Should.ThrowAsync<DbUpdateException>(() => constraintCtx.SaveChangesAsync(ct));
        }

        await using (var collationCtx = fixture.CreateDbContext())
        {
            var collationTable = new CustomTable { Id = Guid.CreateVersion7(), NgoId = ngoId, Name = "CollationTable" };
            var recZz = new CustomRecord { Id = Guid.CreateVersion7(), CustomTableId = collationTable.Id, Position = "Zz", Data = [] };
            var recA0 = new CustomRecord { Id = Guid.CreateVersion7(), CustomTableId = collationTable.Id, Position = "a0", Data = [] };
            var recB00 = new CustomRecord { Id = Guid.CreateVersion7(), CustomTableId = collationTable.Id, Position = "b00", Data = [] };
            collationCtx.AddRange(collationTable, recB00, recZz, recA0);
            await collationCtx.SaveChangesAsync(ct);

            var repo = new CustomRecordRepository(collationCtx);
            var page = await repo.GetPagedAsync<CustomRecordDto>(collationTable.Id, orderBy: null, cancellationToken: ct);
            page.Items.Select(x => x.Id).ShouldBe([recZz.Id, recA0.Id, recB00.Id]);
        }

        // Verify hard cascade delete of NGO removes CustomTable and all CustomRecords cleanly.
        await using var deleteCtx = fixture.CreateDbContext();
        await deleteCtx.Set<Ngo>().Where(n => n.Id == ngoId).ExecuteDeleteAsync(ct);
        (await deleteCtx.Set<CustomRecord>().IgnoreQueryFilters().AnyAsync(r => r.CustomTableId == tableId, ct))
            .ShouldBeFalse();
    }

    private static void AssignPositions(params CustomRecord[] records)
    {
        var ranks = FractionalIndex.GenerateEvenlySpaced(records.Length);
        for (int i = 0; i < records.Length; i++)
        {
            records[i].Position = ranks[i];
        }
    }

    private static AuthUser CreateAuthUser(string email, JsonObject customFields)
    {
        var id = Guid.CreateVersion7();
        return new AuthUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            UserProfile = new User
            {
                Id = id,
                FirstName = "Test",
                LastName = "User",
                IsActive = true,
                CustomFields = customFields,
            },
        };
    }

    public sealed class PostgresFixture : IAsyncLifetime
    {
        private PostgreSqlContainer? _container;

        public bool IsDockerAvailable { get; private set; }

        public DbContextOptions<ApplicationDbContext>? DbContextOptions { get; private set; }

        public ApplicationDbContext CreateDbContext() => new(DbContextOptions!);

        public async ValueTask InitializeAsync()
        {
            try
            {
                _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
                await _container.StartAsync();

                DbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseNpgsql(_container.GetConnectionString())
                    .AddInterceptors(new RowLockCommandInterceptor())
                    .Options;

                await using var context = CreateDbContext();
                await context.Database.MigrateAsync();
                IsDockerAvailable = true;
            }
            catch (Exception)
            {
                IsDockerAvailable = false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }
        }
    }

    private sealed class TestSystemPreset(
        string name,
        string? description,
        List<TemplateFieldDefinition> fields) : ISystemTableTemplate
    {
        public TableTemplate GetTemplate() => new()
        {
            Name = name,
            Description = description,
            IsSystemPreset = true,
            Fields = fields,
        };
    }
}
