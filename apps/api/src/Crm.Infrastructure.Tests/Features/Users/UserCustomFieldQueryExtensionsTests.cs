using Crm.Application.Dtos.CustomTables;
using Crm.Application.Extensions;
using Crm.Application.Features.CustomTables.Mappings;
using Crm.Application.Features.Users.Mappings;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Consts;
using Crm.Infrastructure.Persistence.Extensions;
using Crm.Infrastructure.Persistence.Sorting;
using Crm.Infrastructure.Repositories;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;

namespace Crm.Infrastructure.Tests.Features.Users;

/// <summary>
/// Unit tests verifying PostgreSQL SQL translation for user JSONB custom field smart sorting,
/// NULLS-LAST descending ordering, privacy-partitioned JSONB search, and EF Core model configuration.
/// </summary>
public class UserCustomFieldQueryExtensionsTests
{
    static UserCustomFieldQueryExtensionsTests()
    {
        new UserMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Theory]
    [InlineData(InferredSortKind.Number, PostgresConstants.TryNumericFunction)]
    [InlineData(InferredSortKind.Boolean, PostgresConstants.TryBooleanFunction)]
    [InlineData(InferredSortKind.Date, PostgresConstants.TryDateFunction)]
    [InlineData(InferredSortKind.DateTime, PostgresConstants.TryTimestampTzFunction)]
    public void OrderByUserCustomFieldWhenTypedShouldGeneratePostgresSafeCastFunctionWithoutLanguageCollation(
        InferredSortKind sortKind,
        string expectedFunctionName)
    {
        // Arrange
        using var context = CreateContext();
        var query = context.Set<AuthUser>().AsQueryable();

        // Act
        var ordered = query.OrderByUserCustomField(
            "f_metric",
            SortOrderConstants.Ascending,
            sortKind);
        string sql = ordered.ToQueryString();

        // Assert
        sql.ShouldContain("jsonb_extract_path_text");
        sql.ShouldContain("\"CustomFields\"");
        sql.ShouldContain(expectedFunctionName);
        sql.ShouldNotContain("pg_input_is_valid");
        sql.ShouldNotContain("COLLATE");
        sql.ShouldContain("ORDER BY");
    }

    [Fact]
    public void OrderByUserCustomFieldDescendingShouldSortNullsLastWithDeterministicIdTieBreaker()
    {
        // Arrange
        using var context = CreateContext();
        var query = context.Set<AuthUser>().AsQueryable();

        // Act
        var ordered = query.OrderByUserCustomField(
            "f_score",
            SortOrderConstants.Descending,
            InferredSortKind.Number);
        string sql = ordered.ToQueryString();

        // Assert
        sql.ShouldContain("jsonb_extract_path_text");
        sql.ShouldContain(PostgresConstants.TryNumericFunction);
        sql.ShouldContain("IS NULL");
        sql.ShouldContain("DESC");
        sql.ShouldContain("\"Id\"");
    }

    [Fact]
    public void OrderByUserCustomFieldWhenTextShouldUseDefaultStringOrderingWithoutSafeCastOrCollation()
    {
        // Arrange
        using var context = CreateContext();
        var query = context.Set<AuthUser>().AsQueryable();

        // Act
        var ordered = query.OrderByUserCustomField(
            "f_city",
            SortOrderConstants.Ascending,
            InferredSortKind.Text);
        string sql = ordered.ToQueryString();

        // Assert
        sql.ShouldContain("jsonb_extract_path_text");
        sql.ShouldNotContain("COLLATE");
        sql.ShouldNotContain("crm_try_");
        sql.ShouldContain("ORDER BY");
    }

    [Fact]
    public void BuildVisibleCustomFieldSearchPredicateShouldGenerateJsonbILikeAndScopePrivateKeysToCaller()
    {
        // Arrange
        using var context = CreateContext();
        var callerId = Guid.NewGuid();
        var predicate = UserCustomFieldQueryExtensions.BuildVisibleCustomFieldSearchPredicate(
            "Kyiv",
            publicKeys: ["f_city"],
            privateKeys: ["f_secret_note"],
            currentUserId: callerId);

        predicate.ShouldNotBeNull();

        // Act
        string sql = context.Set<AuthUser>().Where(predicate).ToQueryString();

        // Assert
        sql.ShouldContain("jsonb_extract_path_text");
        sql.ShouldContain("ILIKE");
        sql.ShouldContain("@callerId");
    }

    [Fact]
    public void BuildVisibleCustomFieldSearchPredicateWhenOnlyPublicKeysShouldOmitSelfIdRestriction()
    {
        // Arrange
        using var context = CreateContext();
        var predicate = UserCustomFieldQueryExtensions.BuildVisibleCustomFieldSearchPredicate(
            "Kyiv",
            publicKeys: ["f_city", "f_salary"],
            privateKeys: [],
            currentUserId: Guid.NewGuid());

        predicate.ShouldNotBeNull();

        // Act
        string sql = context.Set<AuthUser>().Where(predicate).ToQueryString();

        // Assert: shares a single @pattern parameter across multiple JSONB keys and omits caller ID filter
        sql.ShouldContain("jsonb_extract_path_text");
        sql.ShouldContain("ILIKE");
        sql.ShouldContain("@pattern");
        sql.ShouldNotContain("@pattern7");
        sql.ShouldNotContain("@callerId");
    }

    [Fact]
    public void BuildVisibleCustomFieldSearchPredicateWhenNoKeysShouldReturnNull()
    {
        var predicate = UserCustomFieldQueryExtensions.BuildVisibleCustomFieldSearchPredicate(
            "Kyiv",
            publicKeys: [],
            privateKeys: [],
            currentUserId: Guid.NewGuid());

        predicate.ShouldBeNull();
    }

    [Fact]
    public void FieldSchemasTemplateJsonbAndCustomRecordPositionShouldBeConfiguredWithoutPendingModelChanges()
    {
        // Arrange
        using var context = CreateContext();
        var userFieldEntity = context.Model.FindEntityType(typeof(UserCustomField))!;
        var customFieldEntity = context.Model.FindEntityType(typeof(CustomField))!;
        var tableTemplateEntity = context.Model.FindEntityType(typeof(TableTemplate))!;
        var templateFieldsNav = tableTemplateEntity.FindNavigation(nameof(TableTemplate.Fields))!;
        var userEntity = context.Model.FindEntityType(typeof(User))!;
        var customFieldsProperty = userEntity.FindProperty(nameof(User.CustomFields))!;
        var customRecordEntity = context.Model.FindEntityType(typeof(CustomRecord))!;
        var positionProp = customRecordEntity.FindProperty(nameof(CustomRecord.Position))!;
        var searchVectorProp = customRecordEntity.FindProperty(CustomRecordSearchConstants.SearchVectorPropertyName)!;

        // Assert: DataType and Key removed everywhere; TemplateField table replaced by owned JSONB collection
        userFieldEntity.FindProperty("DataType").ShouldBeNull();
        userFieldEntity.FindProperty("Key").ShouldBeNull();
        customFieldEntity.FindProperty("DataType").ShouldBeNull();
        customFieldEntity.FindProperty("Key").ShouldBeNull();
        context.Model.FindEntityType("Crm.Domain.Entities.TemplateField").ShouldBeNull();
        templateFieldsNav.TargetEntityType.IsMappedToJson().ShouldBeTrue();

        // Assert: User.CustomFields JSONB column
        customFieldsProperty.GetColumnType().ShouldBe("jsonb");
        customFieldsProperty.IsNullable.ShouldBeFalse();
        customFieldsProperty.GetDefaultValueSql().ShouldBe("'{}'::jsonb");

        // Assert: CustomRecord.Position fractional-index column with COLLATE "C" and unique (CustomTableId, Position) index
        positionProp.IsNullable.ShouldBeFalse();
        positionProp.GetMaxLength().ShouldBe(FractionalIndex.MaxColumnLength);
        var designTimeModel = context.GetService<IDesignTimeModel>().Model;
        var designTimeCustomRecordEntity = designTimeModel.FindEntityType(typeof(CustomRecord))!;
        var designTimeTableTemplateEntity = designTimeModel.FindEntityType(typeof(TableTemplate))!;
        var designTimePositionProp = designTimeCustomRecordEntity.FindProperty(nameof(CustomRecord.Position))!;
        designTimePositionProp.GetCollation().ShouldBe(PostgresConstants.CCollation);
        customRecordEntity.FindProperty("PreviousRecordId").ShouldBeNull();
        customRecordEntity.FindProperty("NextRecordId").ShouldBeNull();
        customRecordEntity.GetIndexes().Any(idx =>
            idx.IsUnique
            && idx.Properties.Select(p => p.Name).SequenceEqual([nameof(CustomRecord.CustomTableId), nameof(CustomRecord.Position)]))
            .ShouldBeTrue();

        searchVectorProp.GetComputedColumnSql().ShouldBe("jsonb_to_tsvector('simple', \"Data\", '[\"string\"]')");

        customRecordEntity.FindProperty(nameof(CustomRecord.Data))!
            .GetDefaultValueSql()
            .ShouldBe("'{}'::jsonb");
        designTimeCustomRecordEntity.GetCheckConstraints()
            .Any(c => c.Name == "CK_CustomRecord_Position_NotEmpty")
            .ShouldBeTrue();
        designTimeTableTemplateEntity.GetCheckConstraints()
            .Any(c => c.Name == "CK_TableTemplate_Fields_IsArray")
            .ShouldBeTrue();

        context.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    [Fact]
    public async Task ApplyFieldSchemaChangesAsyncShouldTrackClientGeneratedAddedFieldsAsAddedAndRemovedAsDeleted()
    {
        new CustomTableMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
        await using var context = CreateContext();

        var keepField = new CustomField { Name = "Keep", IsRequired = false, Order = 0 };
        var removeField = new CustomField { Name = "Remove", IsRequired = false, Order = 1 };
        var table = new CustomTable
        {
            Id = Guid.CreateVersion7(),
            NgoId = Guid.CreateVersion7(),
            Name = "Table",
            Fields = [keepField, removeField],
        };

        context.Attach(table);

        var plan = table.Fields.SynchronizeFields(
        [
            new UpdateCustomFieldDto(keepField.Id, "Keep Updated", IsRequired: true, Order: 0),
            new UpdateCustomFieldDto(null, "Brand New", IsRequired: true, Order: 1),
        ]);

        var repo = new CustomTableRepository(context);
        await repo.ApplyFieldSchemaChangesAsync(
            table.Id,
            plan.AddedFields,
            plan.RemovedFields,
            TestContext.Current.CancellationToken);

        context.ChangeTracker.DetectChanges();

        var addedField = plan.AddedFields.ShouldHaveSingleItem();
        addedField.Id.ShouldNotBe(Guid.Empty);
        addedField.CustomTableId.ShouldBe(table.Id);
        context.Entry(addedField).State.ShouldBe(EntityState.Added);
        context.Entry(keepField).State.ShouldBe(EntityState.Modified);
        context.Entry(removeField).State.ShouldBe(EntityState.Deleted);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;

        return new ApplicationDbContext(options);
    }
}
