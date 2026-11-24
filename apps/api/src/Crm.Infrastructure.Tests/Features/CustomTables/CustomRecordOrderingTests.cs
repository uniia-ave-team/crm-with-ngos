using Crm.Application.Dtos.CustomRecords;
using Crm.Application.Features.CustomRecords.Mappings;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Extensions;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Crm.Infrastructure.Tests.Features.CustomTables;

/// <summary>
/// Unit and integration tests verifying dynamic ordering translation, database functions,
/// key extraction, security validation, and concurrency safety for <see cref="CustomRecordQueryExtensions"/>.
/// </summary>
public class CustomRecordOrderingTests
{
    static CustomRecordOrderingTests()
    {
        new CustomRecordMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public void DbContextModelCreationShouldNotThrowWhenValidatingJsonbExtractFunctions()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        Action act = () => _ = context.Model;

        // Assert
        Should.NotThrow(act);
    }

    [Fact]
    public void OrderByCustomRecordWhenOrderByIdDescendingShouldOrderByIdDescending()
    {
        // Arrange
        using var context = CreateContext();
        var baseQuery = context.Set<CustomRecord>().Where(r => r.CustomTableId == Guid.NewGuid());

        // Act
        var orderedQuery = baseQuery.OrderByCustomRecord("Id", SortOrderConstants.Descending);
        var sql = orderedQuery.ProjectToType<CustomRecordDto>().ToQueryString();

        // Assert
        sql.ShouldNotContain("jsonb_extract_path_text");
        sql.ShouldContain("\"Id\" DESC");
    }

    [Fact]
    public void OrderByCustomRecordWhenStandardPropertyUsedShouldRouteToEntityPropertyOrdering()
    {
        // Arrange
        using var context = CreateContext();
        var baseQuery = context.Set<CustomRecord>().Where(r => r.CustomTableId == Guid.NewGuid());

        // Act
        var orderedQuery = baseQuery.OrderByCustomRecord(nameof(CustomRecord.CustomTableId), SortOrderConstants.Descending, fieldDataType: null);
        var sql = orderedQuery.ToQueryString();

        // Assert
        sql.ShouldNotContain("jsonb_extract_path_text");
        sql.ShouldContain("\"CustomTableId\" DESC");
    }

    [Fact]
    public void OrderByCustomRecordWhenOrderByCustomTableIdWithDtoShouldGenerateCustomTableIdDesc()
    {
        // Arrange
        using var context = CreateContext();
        var query = context.Set<CustomRecord>().Where(r => r.CustomTableId == Guid.NewGuid());

        // Act
        var orderedQuery = query.OrderByCustomRecord(
            nameof(CustomRecord.CustomTableId),
            SortOrderConstants.Descending,
            fieldDataType: null);

        var sql = orderedQuery.ProjectToType<CustomRecordDto>().ToQueryString();

        // Assert
        sql.ShouldContain("\"CustomTableId\" DESC");
        sql.ShouldContain("ORDER BY");
    }

    [Fact]
    public void SortablePropertiesMapShouldCaptureAllScalarPropertiesOfCustomRecord()
    {
        // Arrange
        var scalarProps = typeof(CustomRecord)
            .GetProperties()
            .Where(p => p.Name != nameof(CustomRecord.Data) && p.PropertyType.IsScalarType())
            .Select(p => p.Name)
            .ToList();

        // Act
        var missingSortable = scalarProps.Where(p => !CustomRecordExtensions.SortablePropertiesMap.ContainsKey(p)).ToList();
        var missingReserved = scalarProps.Where(p => !CustomFieldExtensions.IsReservedKey(p)).ToList();

        // Assert
        missingSortable.ShouldBeEmpty();
        missingReserved.ShouldBeEmpty();
    }

    [Fact]
    public void OrderByCustomRecordWhenOrderByJsonKeyAscendingShouldGenerateParameterizedJsonbExtractPathTextAscending()
    {
        // Arrange
        using var context = CreateContext();
        var baseQuery = context.Set<CustomRecord>().Where(r => r.CustomTableId == Guid.NewGuid());

        // Act
        var orderedQuery = baseQuery.OrderByCustomRecord("name", SortOrderConstants.Ascending);
        var sql = orderedQuery.ProjectToType<CustomRecordDto>().ToQueryString();

        // Assert
        sql.ShouldContain("jsonb_extract_path_text");
        sql.ShouldContain("-- @jsonKey='name'");
        sql.ShouldContain("@jsonKey");
        sql.ShouldContain("ORDER BY");
        sql.ShouldNotContain("DESC");
    }

    [Fact]
    public void OrderByCustomRecordWhenOrderByJsonKeyDescendingWithDataPrefixShouldGenerateJsonbExtractPathTextDescending()
    {
        // Arrange
        using var context = CreateContext();
        var baseQuery = context.Set<CustomRecord>().Where(r => r.CustomTableId == Guid.NewGuid());

        // Act
        var orderedQuery = baseQuery.OrderByCustomRecord("data.age", SortOrderConstants.Descending);
        var sql = orderedQuery.ProjectToType<CustomRecordDto>().ToQueryString();

        // Assert
        sql.ShouldContain("jsonb_extract_path_text");
        sql.ShouldContain("'age'");
        sql.ShouldContain("DESC");
    }

    [Fact]
    public void OrderByCustomRecordWhenOrderByIndexerNotationShouldExtractKeyCorrectly()
    {
        // Arrange
        using var context = CreateContext();
        var baseQuery = context.Set<CustomRecord>().Where(r => r.CustomTableId == Guid.NewGuid());

        // Act
        var orderedQuery = baseQuery.OrderByCustomRecord("data[\"status\"]", SortOrderConstants.Descending);
        var sql = orderedQuery.ProjectToType<CustomRecordDto>().ToQueryString();

        // Assert
        sql.ShouldContain("jsonb_extract_path_text");
        sql.ShouldContain("'status'");
        sql.ShouldContain("DESC");
    }

    [Theory]
    [InlineData(FieldDataType.Number, "numeric")]
    [InlineData(FieldDataType.Boolean, "boolean")]
    [InlineData(FieldDataType.Date, "date")]
    [InlineData(FieldDataType.DateTime, "timestamp with time zone")]
    public void OrderByCustomRecordWhenTypedFieldShouldGeneratePgInputIsValidAndCorrectCast(
        FieldDataType dataType,
        string expectedSqlStoreType)
    {
        // Arrange
        using var context = CreateContext();
        var baseQuery = context.Set<CustomRecord>().Where(r => r.CustomTableId == Guid.NewGuid());

        // Act
        var orderedQuery = baseQuery.OrderByCustomRecord(
            "data.custom_val",
            SortOrderConstants.Ascending,
            dataType);
        var sql = orderedQuery.ProjectToType<CustomRecordDto>().ToQueryString();

        // Assert
        sql.ShouldContain("pg_input_is_valid");
        sql.ShouldContain("jsonb_extract_path_text");

        if (dataType == FieldDataType.Date)
        {
            sql.ShouldContain("timestamp with time zone");
            sql.ShouldContain("timezone('UTC'");
            sql.ShouldContain("date");
        }
        else
        {
            sql.ShouldContain($"'{expectedSqlStoreType}'");
        }

        if (dataType is FieldDataType.Number or FieldDataType.Date or FieldDataType.DateTime)
        {
            sql.ShouldContain("regexp_like");
            sql.ShouldContain("Infinity");
        }
    }

    [Theory]
    [InlineData("invalid key; DROP TABLE Users;")]
    [InlineData("data.key; DROP TABLE Users;")]
    [InlineData("data['key--comment']")]
    [InlineData("data.key/*comment*/")]
    [InlineData("data.nested.path.value")]
    [InlineData("data[0]")]
    [InlineData("_status")]
    [InlineData("data['_status']")]
    public void OrderByCustomRecordWithMaliciousOrDeepKeysShouldFallbackSafelyToOrderById(string maliciousOrderBy)
    {
        // Arrange
        using var context = CreateContext();
        var query = context.Set<CustomRecord>().AsQueryable();

        // Act
        var result = query.OrderByCustomRecord(maliciousOrderBy, SortOrderConstants.Ascending, FieldDataType.Text);
        var sql = result.ToQueryString();

        // Assert
        sql.ShouldNotContain("DROP TABLE");
        sql.ShouldNotContain("jsonb_extract_path_text");
        sql.ShouldContain("\"Id\"");
    }

    [Theory]
    [InlineData("data['valid_field']", "valid_field")]
    [InlineData("data[\"valid_field\"]", "valid_field")]
    [InlineData("data.valid_field", "valid_field")]
    [InlineData("data[ 'valid_field' ]", "valid_field")]
    [InlineData("data [ \"valid_field\" ]", "valid_field")]
    [InlineData("data [ 'priority' ]", "priority")]
    [InlineData("data[  \"priority\"  ]", "priority")]
    [InlineData("priority", "priority")]
    [InlineData("valid_field", "valid_field")]
    public void ExtractKeyValidInputsShouldExtractCorrectly(string input, string expected)
    {
        // Arrange
        var targetInput = input;

        // Act
        var result = CustomFieldExtensions.ExtractKey(targetInput);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("data['_leading_underscore']")]
    [InlineData("data['123_starts_with_digit']")]
    [InlineData("data['ім_я']")]
    [InlineData("data.key; DROP TABLE Users;")]
    [InlineData("data.nested.path.val")]
    [InlineData("Id")]
    [InlineData("CustomTableId")]
    [InlineData("Data")]
    public void ExtractKeyInvalidOrReservedKeysShouldReturnNull(string input)
    {
        // Arrange
        var targetInput = input;

        // Act
        var result = CustomFieldExtensions.ExtractKey(targetInput);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void FindFieldByOrderByShouldMatchTolerantly()
    {
        // Arrange
        var fields = new List<CustomField>
        {
            new() { Key = "priority", DataType = FieldDataType.Number },
            new() { Key = "status_code", DataType = FieldDataType.Text },
        };

        // Act
        var priorityField = fields.FindFieldByOrderBy("data['priority']");
        var statusCodeField = fields.FindFieldByOrderBy("data[  \"status_code\"  ]");
        var nonExistentField = fields.FindFieldByOrderBy("non_existent");

        // Assert
        priorityField?.Key.ShouldBe("priority");
        statusCodeField?.Key.ShouldBe("status_code");
        nonExistentField.ShouldBeNull();
    }

    [Fact]
    public void OrderByCustomRecordConcurrentCallsShouldSafelyGenerateParameterizedSql()
    {
        // Arrange
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Act
        Parallel.For(0, 50, _ =>
        {
            try
            {
                using var context = CreateContext();
                var query = context.Set<CustomRecord>().AsQueryable();

                var orderedQuery = query.OrderByCustomRecord("data.priority", SortOrderConstants.Descending, FieldDataType.Number);
                var sql = orderedQuery.ToQueryString();

                if (!sql.Contains("jsonb_extract_path_text") || !sql.Contains("\"Id\""))
                {
                    exceptions.Add(new InvalidOperationException($"Unexpected SQL generated: {sql}"));
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Assert
        exceptions.ShouldBeEmpty();
    }

    [Fact]
    public void FilterByTableAndSearchWhenSearchTermProvidedShouldGenerateToTsQueryWithPrefix()
    {
        // Arrange
        using var context = CreateContext();
        var query = context.Set<CustomRecord>().AsQueryable();

        // Act
        var filtered = query.FilterByTableAndSearch(Guid.NewGuid(), "Kyi");
        var sql = filtered.ToQueryString();

        // Assert
        sql.ShouldContain("to_tsquery('simple'");
        sql.ShouldContain("kyi:*");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;

        return new ApplicationDbContext(options);
    }
}
