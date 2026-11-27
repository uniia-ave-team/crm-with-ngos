using Crm.Application.Dtos.CustomRecords;
using Crm.Application.Features.CustomRecords.Mappings;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.Extensions;
using Crm.Infrastructure.Persistence.Sorting;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Crm.Infrastructure.Tests.Features.CustomTables;

/// <summary>
/// Unit tests verifying dynamic smart ordering translation via PostgreSQL safe-cast SQL functions,
/// language-agnostic text ordering, GUID field-ID validation, security validation, and concurrency safety.
/// </summary>
public class CustomRecordOrderingTests
{
    private const string SampleFieldId = "019a0000-0000-7000-8000-000000000001";

    static CustomRecordOrderingTests()
    {
        new CustomRecordMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public void DbContextModelCreationShouldNotThrowWhenValidatingCustomDbFunctions()
    {
        using var context = CreateContext();

        Action act = () => _ = context.Model;

        Should.NotThrow(act);
    }

    [Fact]
    public void OrderByCustomRecordWhenOrderByIdDescendingShouldOrderByIdDescending()
    {
        using var context = CreateContext();
        var baseQuery = context.Set<CustomRecord>().Where(r => r.CustomTableId == Guid.NewGuid());

        var orderedQuery = baseQuery.OrderByCustomRecord("Id", SortOrderConstants.Descending);
        var sql = orderedQuery.ProjectToType<CustomRecordDto>().ToQueryString();

        sql.ShouldNotContain("jsonb_extract_path_text");
        sql.ShouldContain("\"Id\" DESC");
    }

    [Fact]
    public void OrderByCustomRecordWhenStandardPropertyUsedShouldRouteToEntityPropertyOrdering()
    {
        using var context = CreateContext();
        var baseQuery = context.Set<CustomRecord>().Where(r => r.CustomTableId == Guid.NewGuid());

        var orderedQuery = baseQuery.OrderByCustomRecord(nameof(CustomRecord.CustomTableId), SortOrderConstants.Descending);
        var sql = orderedQuery.ToQueryString();

        sql.ShouldNotContain("jsonb_extract_path_text");
        sql.ShouldContain("\"CustomTableId\" DESC");
    }

    [Fact]
    public void OrderByCustomRecordWhenOrderByPositionShouldRouteToPositionColumnOrdering()
    {
        using var context = CreateContext();
        var query = context.Set<CustomRecord>().Where(r => r.CustomTableId == Guid.NewGuid());

        var orderedQuery = query.OrderByCustomRecord(
            nameof(CustomRecord.Position),
            SortOrderConstants.Descending);

        var sql = orderedQuery.ProjectToType<CustomRecordDto>().ToQueryString();

        sql.ShouldNotContain("jsonb_extract_path_text");
        sql.ShouldContain("\"Position\" DESC");
        sql.ShouldContain("ORDER BY");
    }

    [Fact]
    public void SortablePropertiesMapShouldCaptureAllScalarPropertiesOfCustomRecord()
    {
        var scalarProps = typeof(CustomRecord)
            .GetProperties()
            .Where(p => p.Name != nameof(CustomRecord.Data) && p.PropertyType.IsScalarType())
            .Select(p => p.Name)
            .ToList();

        var missingSortable = scalarProps.Where(p => !CustomRecordExtensions.SortablePropertiesMap.ContainsKey(p)).ToList();

        missingSortable.ShouldBeEmpty();
        CustomRecordExtensions.SortablePropertiesMap.ContainsKey(nameof(CustomRecord.Position)).ShouldBeTrue();
    }

    [Fact]
    public void OrderByCustomRecordWhenOrderByFieldIdAscendingShouldGenerateLanguageAgnosticParameterizedSql()
    {
        using var context = CreateContext();
        var baseQuery = context.Set<CustomRecord>().Where(r => r.CustomTableId == Guid.NewGuid());

        var orderedQuery = baseQuery.OrderByCustomRecord(SampleFieldId.ToUpperInvariant(), SortOrderConstants.Ascending);
        var sql = orderedQuery.ProjectToType<CustomRecordDto>().ToQueryString();

        sql.ShouldContain("jsonb_extract_path_text");
        sql.ShouldNotContain("COLLATE");
        sql.ShouldContain(SampleFieldId);
        sql.ShouldContain("ORDER BY");
        sql.ShouldNotContain("DESC");
    }

    [Theory]
    [InlineData(InferredSortKind.Number, PostgresConstants.TryNumericFunction)]
    [InlineData(InferredSortKind.Boolean, PostgresConstants.TryBooleanFunction)]
    [InlineData(InferredSortKind.Date, PostgresConstants.TryDateFunction)]
    [InlineData(InferredSortKind.DateTime, PostgresConstants.TryTimestampTzFunction)]
    public void OrderByCustomRecordWhenInferredTypedSortShouldInvokeDedicatedPostgresSafeCastFunctionWithoutLanguageCollation(
        InferredSortKind sortKind,
        string expectedFunctionName)
    {
        using var context = CreateContext();
        var baseQuery = context.Set<CustomRecord>().Where(r => r.CustomTableId == Guid.NewGuid());

        var orderedQuery = baseQuery.OrderByCustomRecord(
            SampleFieldId,
            SortOrderConstants.Ascending,
            sortKind);
        var sql = orderedQuery.ProjectToType<CustomRecordDto>().ToQueryString();

        sql.ShouldContain(expectedFunctionName);
        sql.ShouldContain("jsonb_extract_path_text");
        sql.ShouldNotContain("pg_input_is_valid");
        sql.ShouldNotContain("COLLATE");
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("f_status")]
    [InlineData("invalid key; DROP TABLE Users;")]
    [InlineData("data.key")]
    [InlineData("data['status']")]
    [InlineData("data.key; DROP TABLE Users;")]
    [InlineData("data.nested.path.value")]
    public void OrderByCustomRecordWithMaliciousOrNonGuidKeysShouldFallbackSafelyToOrderById(string maliciousOrderBy)
    {
        using var context = CreateContext();
        var query = context.Set<CustomRecord>().AsQueryable();

        var result = query.OrderByCustomRecord(maliciousOrderBy, SortOrderConstants.Ascending, InferredSortKind.Text);
        var sql = result.ToQueryString();

        sql.ShouldNotContain("DROP TABLE");
        sql.ShouldNotContain("jsonb_extract_path_text");
        sql.ShouldContain("\"Id\"");
    }

    [Fact]
    public void FindFieldByIdShouldMatchGuidCaseInsensitively()
    {
        var priorityField = new CustomField { Name = "Priority" };
        var statusField = new CustomField { Name = "Status" };
        var fields = new List<CustomField> { priorityField, statusField };

        fields.FindFieldById(priorityField.Id.ToString())?.Id.ShouldBe(priorityField.Id);
        fields.FindFieldById(statusField.Id.ToString().ToUpperInvariant())?.Id.ShouldBe(statusField.Id);
        fields.FindFieldById($"data['{priorityField.Id}']").ShouldBeNull();
        fields.FindFieldById(Guid.CreateVersion7().ToString()).ShouldBeNull();
    }

    [Fact]
    public void OrderByCustomRecordConcurrentCallsShouldSafelyGenerateParameterizedSql()
    {
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        Parallel.For(0, 50, _ =>
        {
            try
            {
                using var context = CreateContext();
                var query = context.Set<CustomRecord>().AsQueryable();

                var orderedQuery = query.OrderByCustomRecord(SampleFieldId, SortOrderConstants.Descending, InferredSortKind.Number);
                var sql = orderedQuery.ToQueryString();

                if (!sql.Contains("jsonb_extract_path_text") || !sql.Contains(PostgresConstants.TryNumericFunction) || !sql.Contains("\"Id\""))
                {
                    exceptions.Add(new InvalidOperationException($"Unexpected SQL generated: {sql}"));
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        exceptions.ShouldBeEmpty();
    }

    [Fact]
    public void FilterByTableAndSearchWhenSearchTermProvidedShouldGenerateToTsQueryWithPrefix()
    {
        using var context = CreateContext();
        var query = context.Set<CustomRecord>().AsQueryable();

        var filtered = query.FilterByTableAndSearch(Guid.NewGuid(), "Kyi");
        var sql = filtered.ToQueryString();

        sql.ShouldContain("to_tsquery('simple'");
        sql.ShouldContain("kyi:*");
    }

    [Fact]
    public void BuildSampleQueryShouldPreserveDeterministicOrderingBeforeLimit()
    {
        using var context = CreateContext();
        var tableId = Guid.CreateVersion7();

        IOrderedQueryable<CustomRecord> ordered = context.Set<CustomRecord>()
            .AsNoTracking()
            .Where(r => r.CustomTableId == tableId)
            .OrderBy(r => r.Position)
            .ThenBy(r => r.Id);

        string sql = ordered
            .BuildSampleQuery(r => JsonbFunctionsExtensions.JsonbExtractPathText(r.Data, SampleFieldId))
            .ToQueryString();

        sql.ShouldContain("jsonb_extract_path_text");
        sql.ShouldContain("IS NOT NULL");
        sql.ShouldContain("ORDER BY");
        sql.ShouldContain("\"Position\"");
        sql.ShouldContain("\"Id\"");
        sql.ShouldContain("LIMIT");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;

        return new ApplicationDbContext(options);
    }
}
