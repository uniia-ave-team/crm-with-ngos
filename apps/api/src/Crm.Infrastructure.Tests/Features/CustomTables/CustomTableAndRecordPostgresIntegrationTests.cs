using System.Text.Json.Nodes;
using Crm.Application.Dtos.CustomRecords;
using Crm.Application.Dtos.CustomTables;
using Crm.Application.Dtos.CustomTables.Commands;
using Crm.Application.Dtos.CustomTemplates;
using Crm.Application.Dtos.CustomTemplates.Commands;
using Crm.Application.Features.CustomRecords.Mappings;
using Crm.Application.Features.CustomTables.Commands;
using Crm.Application.Features.CustomTables.Mappings;
using Crm.Application.Features.CustomTemplates.Commands;
using Crm.Application.Features.CustomTemplates.Mappings;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Repositories;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Crm.Infrastructure.Tests.Features.CustomTables;

/// <summary>
/// Executes real SQL integration tests against a PostgreSQL Testcontainer instance
/// to verify schema synchronization, key-based field matching without ID, key swapping,
/// JSONB value-only prefix full-text search, and typed dynamic sorting.
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
    }

    [Fact]
    public async Task UpdateCustomTableWhenFieldHasExistingKeyWithoutIdShouldPreserveIdAndBackfillMissingValuesInPostgres()
    {
        // Arrange
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var setupContext = fixture.CreateDbContext();
        var ngo = new Ngo { Id = Guid.NewGuid(), Name = $"NGO-{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        var cityFieldId = Guid.NewGuid();
        var table = new CustomTable
        {
            Id = Guid.NewGuid(),
            NgoId = ngo.Id,
            Name = "Beneficiaries",
            Fields =
            [
                new CustomField
                {
                    Id = cityFieldId,
                    Key = "city",
                    Name = "City",
                    DataType = FieldDataType.Text,
                    IsRequired = false,
                    Order = 0,
                },
            ],
        };

        var recordWithCity = new CustomRecord
        {
            Id = Guid.NewGuid(),
            CustomTableId = table.Id,
            Data = new JsonObject { ["city"] = "Kyiv" },
        };
        var recordWithoutCity = new CustomRecord
        {
            Id = Guid.NewGuid(),
            CustomTableId = table.Id,
            Data = [],
        };

        setupContext.AddRange(ngo, table, recordWithCity, recordWithoutCity);
        await setupContext.SaveChangesAsync(ct);

        // Act
        await using var actContext = fixture.CreateDbContext();
        await using var unitOfWork = new UnitOfWork(actContext);
        var tableRepo = new CustomTableRepository(actContext);
        var recordRepo = new CustomRecordRepository(actContext);
        var handler = new UpdateCustomTableCommandHandler(tableRepo, recordRepo, NullLogger<UpdateCustomTableCommandHandler>.Instance);

        var command = new UpdateCustomTableCommand(
            table.Id,
            "Beneficiaries Updated",
            "Updated description",
            [
                new UpdateCustomFieldDto(null, "City (Mandatory)", "city", FieldDataType.Text, true, 0),
                new UpdateCustomFieldDto(null, "Notes", "notes", FieldDataType.Text, false, 1),
            ]);

        await unitOfWork.BeginTransactionAsync(ct);
        await handler.Handle(command, ct);
        await unitOfWork.CommitTransactionAsync(ct);

        // Assert
        await using var assertContext = fixture.CreateDbContext();
        var persistedTable = await assertContext.Set<CustomTable>()
            .Include(t => t.Fields)
            .SingleAsync(t => t.Id == table.Id, ct);

        persistedTable.Name.ShouldBe("Beneficiaries Updated");
        persistedTable.Fields.Count.ShouldBe(2);

        var persistedCityField = persistedTable.Fields.Single(f => f.Key == "city");
        persistedCityField.Id.ShouldBe(cityFieldId);
        persistedCityField.Name.ShouldBe("City (Mandatory)");
        persistedCityField.IsRequired.ShouldBeTrue();

        var records = await assertContext.Set<CustomRecord>()
            .Where(r => r.CustomTableId == table.Id)
            .ToDictionaryAsync(r => r.Id, ct);

        records[recordWithCity.Id].Data["city"]?.GetValue<string>().ShouldBe("Kyiv");
        records[recordWithoutCity.Id].Data["city"]?.GetValue<string>().ShouldBe(string.Empty);
        records[recordWithCity.Id].Data.ContainsKey("notes").ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateCustomTableWhenFieldHasExistingKeyWithoutIdAndDifferentDataTypeShouldReplaceFieldAndResetValuesInPostgres()
    {
        // Arrange
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var setupContext = fixture.CreateDbContext();
        var ngo = new Ngo { Id = Guid.NewGuid(), Name = $"NGO-{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        var oldFieldId = Guid.NewGuid();
        var table = new CustomTable
        {
            Id = Guid.NewGuid(),
            NgoId = ngo.Id,
            Name = "Locations",
            Fields =
            [
                new CustomField
                {
                    Id = oldFieldId,
                    Key = "city",
                    Name = "City Name",
                    DataType = FieldDataType.Text,
                    IsRequired = true,
                    Order = 0,
                },
            ],
        };

        var record = new CustomRecord
        {
            Id = Guid.NewGuid(),
            CustomTableId = table.Id,
            Data = new JsonObject { ["city"] = "Kyiv" },
        };

        setupContext.AddRange(ngo, table, record);
        await setupContext.SaveChangesAsync(ct);

        // Act
        await using var actContext = fixture.CreateDbContext();
        await using var unitOfWork = new UnitOfWork(actContext);
        var tableRepo = new CustomTableRepository(actContext);
        var recordRepo = new CustomRecordRepository(actContext);
        var handler = new UpdateCustomTableCommandHandler(tableRepo, recordRepo, NullLogger<UpdateCustomTableCommandHandler>.Instance);

        var command = new UpdateCustomTableCommand(
            table.Id,
            "Locations",
            null,
            [
                new UpdateCustomFieldDto(null, "City Code", "city", FieldDataType.Number, true, 0),
            ]);

        await unitOfWork.BeginTransactionAsync(ct);
        await handler.Handle(command, ct);
        await unitOfWork.CommitTransactionAsync(ct);

        // Assert
        await using var assertContext = fixture.CreateDbContext();
        var persistedField = await assertContext.Set<CustomField>()
            .SingleAsync(f => f.CustomTableId == table.Id && f.Key == "city", ct);

        persistedField.Id.ShouldNotBe(oldFieldId);
        persistedField.DataType.ShouldBe(FieldDataType.Number);
        persistedField.Name.ShouldBe("City Code");

        var persistedRecord = await assertContext.Set<CustomRecord>()
            .SingleAsync(r => r.Id == record.Id, ct);

        persistedRecord.Data["city"]?.GetValue<int>().ShouldBe(0);
    }

    [Fact]
    public async Task UpdateCustomTableWhenTwoFieldsSwapKeysShouldPersistBothFieldsAndSwapRecordJsonKeysInPostgres()
    {
        // Arrange
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var setupContext = fixture.CreateDbContext();
        var ngo = new Ngo { Id = Guid.NewGuid(), Name = $"NGO-{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        var fieldAId = Guid.NewGuid();
        var fieldBId = Guid.NewGuid();
        var table = new CustomTable
        {
            Id = Guid.NewGuid(),
            NgoId = ngo.Id,
            Name = "Inventory",
            Fields =
            [
                new CustomField
                {
                    Id = fieldAId,
                    Key = "primary_code",
                    Name = "Primary Code",
                    DataType = FieldDataType.Text,
                    IsRequired = true,
                    Order = 0,
                },
                new CustomField
                {
                    Id = fieldBId,
                    Key = "secondary_code",
                    Name = "Secondary Code",
                    DataType = FieldDataType.Text,
                    IsRequired = true,
                    Order = 1,
                },
            ],
        };

        var record = new CustomRecord
        {
            Id = Guid.NewGuid(),
            CustomTableId = table.Id,
            Data = new JsonObject
            {
                ["primary_code"] = "ALPHA-1",
                ["secondary_code"] = "BETA-2",
            },
        };

        setupContext.AddRange(ngo, table, record);
        await setupContext.SaveChangesAsync(ct);

        // Act
        await using var actContext = fixture.CreateDbContext();
        await using var unitOfWork = new UnitOfWork(actContext);
        var tableRepo = new CustomTableRepository(actContext);
        var recordRepo = new CustomRecordRepository(actContext);
        var handler = new UpdateCustomTableCommandHandler(tableRepo, recordRepo, NullLogger<UpdateCustomTableCommandHandler>.Instance);

        var command = new UpdateCustomTableCommand(
            table.Id,
            "Inventory",
            null,
            [
                new UpdateCustomFieldDto(fieldAId, "Primary Code", "secondary_code", FieldDataType.Text, true, 0),
                new UpdateCustomFieldDto(fieldBId, "Secondary Code", "primary_code", FieldDataType.Text, true, 1),
            ]);

        await unitOfWork.BeginTransactionAsync(ct);
        await handler.Handle(command, ct);
        await unitOfWork.CommitTransactionAsync(ct);

        // Assert
        await using var assertContext = fixture.CreateDbContext();
        var persistedFields = await assertContext.Set<CustomField>()
            .Where(f => f.CustomTableId == table.Id)
            .ToDictionaryAsync(f => f.Id, ct);

        persistedFields[fieldAId].Key.ShouldBe("secondary_code");
        persistedFields[fieldBId].Key.ShouldBe("primary_code");

        var persistedRecord = await assertContext.Set<CustomRecord>()
            .SingleAsync(r => r.Id == record.Id, ct);

        persistedRecord.Data["secondary_code"]?.GetValue<string>().ShouldBe("ALPHA-1");
        persistedRecord.Data["primary_code"]?.GetValue<string>().ShouldBe("BETA-2");
    }

    [Fact]
    public async Task UpdateTableTemplateWhenFieldMatchedByKeyWithoutIdAndKeysSwappedShouldSucceedInPostgres()
    {
        // Arrange
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var setupContext = fixture.CreateDbContext();
        var fieldOneId = Guid.NewGuid();
        var fieldTwoId = Guid.NewGuid();
        var template = new TableTemplate
        {
            Id = Guid.NewGuid(),
            Name = $"Template-{Guid.NewGuid():N}",
            IsSystemPreset = false,
            Fields =
            [
                new TemplateField
                {
                    Id = fieldOneId,
                    Key = "first_key",
                    Name = "First",
                    DataType = FieldDataType.Text,
                    IsRequired = false,
                    Order = 0,
                },
                new TemplateField
                {
                    Id = fieldTwoId,
                    Key = "second_key",
                    Name = "Second",
                    DataType = FieldDataType.Number,
                    IsRequired = true,
                    Order = 1,
                },
            ],
        };

        setupContext.Add(template);
        await setupContext.SaveChangesAsync(ct);

        // Act 1: Match existing field by key without ID (Id = null)
        await using (var actContext1 = fixture.CreateDbContext())
        {
            await using var uow1 = new UnitOfWork(actContext1);
            var repo1 = new TableTemplateRepository(actContext1);
            var handler1 = new UpdateTableTemplateCommandHandler(repo1, NullLogger<UpdateTableTemplateCommandHandler>.Instance);

            var updateWithNullIdCommand = new UpdateTableTemplateCommand(
                template.Id,
                template.Name,
                "Updated",
                [
                    new UpdateTemplateFieldDto(null, "First Updated", "first_key", FieldDataType.Text, true, 0),
                    new UpdateTemplateFieldDto(fieldTwoId, "Second", "second_key", FieldDataType.Number, true, 1),
                ]);

            await uow1.BeginTransactionAsync(ct);
            await handler1.Handle(updateWithNullIdCommand, ct);
            await uow1.CommitTransactionAsync(ct);
        }

        // Act 2: Swap keys of the two template fields
        await using (var actContext2 = fixture.CreateDbContext())
        {
            await using var uow2 = new UnitOfWork(actContext2);
            var repo2 = new TableTemplateRepository(actContext2);
            var handler2 = new UpdateTableTemplateCommandHandler(repo2, NullLogger<UpdateTableTemplateCommandHandler>.Instance);

            var swapKeysCommand = new UpdateTableTemplateCommand(
                template.Id,
                template.Name,
                "Swapped",
                [
                    new UpdateTemplateFieldDto(fieldOneId, "First Updated", "second_key", FieldDataType.Text, true, 0),
                    new UpdateTemplateFieldDto(fieldTwoId, "Second", "first_key", FieldDataType.Number, true, 1),
                ]);

            await uow2.BeginTransactionAsync(ct);
            await handler2.Handle(swapKeysCommand, ct);
            await uow2.CommitTransactionAsync(ct);
        }

        // Assert
        await using var assertContext = fixture.CreateDbContext();
        var persistedFields = await assertContext.Set<TemplateField>()
            .Where(f => f.TemplateId == template.Id)
            .ToDictionaryAsync(f => f.Id, ct);

        persistedFields[fieldOneId].Id.ShouldBe(fieldOneId);
        persistedFields[fieldOneId].Name.ShouldBe("First Updated");
        persistedFields[fieldOneId].IsRequired.ShouldBeTrue();
        persistedFields[fieldOneId].Key.ShouldBe("second_key");
        persistedFields[fieldTwoId].Key.ShouldBe("first_key");
    }

    [Fact]
    public async Task GetPagedAsyncSearchShouldMatchValuePrefixesAndIgnoreJsonKeyNamesInPostgres()
    {
        // Arrange
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var context = fixture.CreateDbContext();
        var ngo = new Ngo { Id = Guid.NewGuid(), Name = $"NGO-{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        var table = new CustomTable
        {
            Id = Guid.NewGuid(),
            NgoId = ngo.Id,
            Name = "Donors",
        };

        var kyivRecord = new CustomRecord
        {
            Id = Guid.NewGuid(),
            CustomTableId = table.Id,
            Data = new JsonObject
            {
                ["city"] = "Kyiv",
                ["organization"] = "Charity Foundation",
                ["amount"] = 1500,
            },
        };
        var lvivRecord = new CustomRecord
        {
            Id = Guid.NewGuid(),
            CustomTableId = table.Id,
            Data = new JsonObject
            {
                ["city"] = "Lviv",
                ["organization"] = "Volunteer Network",
                ["amount"] = 450,
            },
        };

        context.AddRange(ngo, table, kyivRecord, lvivRecord);
        await context.SaveChangesAsync(ct);

        var repository = new CustomRecordRepository(context);

        // Act & Assert 1: Searching by JSON key names ("city", "amount") must NOT match records
        var byCityKey = await repository.GetPagedAsync<CustomRecordDto>(table.Id, searchTerm: "city", cancellationToken: ct);
        byCityKey.TotalCount.ShouldBe(0);

        var byAmountKey = await repository.GetPagedAsync<CustomRecordDto>(table.Id, searchTerm: "amount", cancellationToken: ct);
        byAmountKey.TotalCount.ShouldBe(0);

        // Act & Assert 2: Prefix search ("Kyi") must match "Kyiv"
        var byPrefix = await repository.GetPagedAsync<CustomRecordDto>(table.Id, searchTerm: "Kyi", cancellationToken: ct);
        byPrefix.TotalCount.ShouldBe(1);
        byPrefix.Items.Single().Id.ShouldBe(kyivRecord.Id);

        // Act & Assert 3: Multi-token prefix search ("Kyi Char") must match record containing both prefixes
        var byMultiPrefix = await repository.GetPagedAsync<CustomRecordDto>(table.Id, searchTerm: "Kyi Char", cancellationToken: ct);
        byMultiPrefix.TotalCount.ShouldBe(1);
        byMultiPrefix.Items.Single().Id.ShouldBe(kyivRecord.Id);

        // Act & Assert 4: Numeric prefix search ("150") must match 1500
        var byNumericPrefix = await repository.GetPagedAsync<CustomRecordDto>(table.Id, searchTerm: "150", cancellationToken: ct);
        byNumericPrefix.TotalCount.ShouldBe(1);
        byNumericPrefix.Items.Single().Id.ShouldBe(kyivRecord.Id);
    }

    [Fact]
    public async Task GetPagedAsyncOrderingByNumberAndDateShouldSortByTypeAndPutNullsLastInBothDirectionsInPostgres()
    {
        // Arrange
        if (!fixture.IsDockerAvailable)
        {
            Assert.Skip("Docker is not running on host machine.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        await using var context = fixture.CreateDbContext();
        var ngo = new Ngo { Id = Guid.NewGuid(), Name = $"NGO-{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        var table = new CustomTable
        {
            Id = Guid.NewGuid(),
            NgoId = ngo.Id,
            Name = "Transactions",
        };

        var recSmall = new CustomRecord
        {
            Id = Guid.NewGuid(),
            CustomTableId = table.Id,
            Data = new JsonObject { ["amount"] = 2, ["tx_date"] = "2026-01-15" },
        };
        var recMedium = new CustomRecord
        {
            Id = Guid.NewGuid(),
            CustomTableId = table.Id,
            Data = new JsonObject { ["amount"] = 10, ["tx_date"] = "2026-05-20" },
        };
        var recLarge = new CustomRecord
        {
            Id = Guid.NewGuid(),
            CustomTableId = table.Id,
            Data = new JsonObject { ["amount"] = 100, ["tx_date"] = "2026-10-01" },
        };
        var recNull = new CustomRecord
        {
            Id = Guid.NewGuid(),
            CustomTableId = table.Id,
            Data = new JsonObject { ["amount"] = null },
        };

        context.AddRange(ngo, table, recMedium, recNull, recLarge, recSmall);
        await context.SaveChangesAsync(ct);

        var repository = new CustomRecordRepository(context);

        // Act: Sort by numeric amount ASC and DESC
        var ascNumbers = await repository.GetPagedAsync<CustomRecordDto>(
            table.Id,
            orderBy: "amount",
            fieldDataType: FieldDataType.Number,
            sortOrder: SortOrderConstants.Ascending,
            cancellationToken: ct);

        var descNumbers = await repository.GetPagedAsync<CustomRecordDto>(
            table.Id,
            orderBy: "amount",
            fieldDataType: FieldDataType.Number,
            sortOrder: SortOrderConstants.Descending,
            cancellationToken: ct);

        // Assert: Numeric order (2, 10, 100) with nulls last in both ASC and DESC
        ascNumbers.Items.Select(x => x.Id).ShouldBe([recSmall.Id, recMedium.Id, recLarge.Id, recNull.Id]);
        descNumbers.Items.Select(x => x.Id).ShouldBe([recLarge.Id, recMedium.Id, recSmall.Id, recNull.Id]);

        // Act: Sort by date ASC and DESC
        var ascDates = await repository.GetPagedAsync<CustomRecordDto>(
            table.Id,
            orderBy: "tx_date",
            fieldDataType: FieldDataType.Date,
            sortOrder: SortOrderConstants.Ascending,
            cancellationToken: ct);

        var descDates = await repository.GetPagedAsync<CustomRecordDto>(
            table.Id,
            orderBy: "tx_date",
            fieldDataType: FieldDataType.Date,
            sortOrder: SortOrderConstants.Descending,
            cancellationToken: ct);

        // Assert: Chronological order with nulls last in both ASC and DESC
        ascDates.Items.Select(x => x.Id).ShouldBe([recSmall.Id, recMedium.Id, recLarge.Id, recNull.Id]);
        descDates.Items.Select(x => x.Id).ShouldBe([recLarge.Id, recMedium.Id, recSmall.Id, recNull.Id]);
    }

    public sealed class PostgresFixture : IAsyncLifetime
    {
        private PostgreSqlContainer? _container;

        public bool IsDockerAvailable { get; private set; }

        public DbContextOptions<ApplicationDbContext> DbContextOptions { get; private set; }

        public ApplicationDbContext CreateDbContext() => new(DbContextOptions);

        public async ValueTask InitializeAsync()
        {
            try
            {
                _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
                await _container.StartAsync();

                DbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseNpgsql(_container.GetConnectionString())
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
}
