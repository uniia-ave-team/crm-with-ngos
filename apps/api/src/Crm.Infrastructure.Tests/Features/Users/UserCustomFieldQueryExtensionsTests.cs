using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Mappings;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Extensions;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Crm.Infrastructure.Tests.Features.Users;

/// <summary>
/// Unit tests verifying PostgreSQL SQL translation for user JSONB custom field typed sorting,
/// NULLS-LAST descending ordering, privacy-partitioned JSONB search, EF Core configuration, and reserved keys.
/// </summary>
public class UserCustomFieldQueryExtensionsTests
{
    static UserCustomFieldQueryExtensionsTests()
    {
        new UserMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Theory]
    [InlineData(FieldDataType.Number, "numeric")]
    [InlineData(FieldDataType.Boolean, "boolean")]
    [InlineData(FieldDataType.Date, "date")]
    [InlineData(FieldDataType.DateTime, "timestamp with time zone")]
    public void OrderByUserCustomFieldWhenTypedShouldGenerateJsonbExtractPathTextAndSafeCast(
        FieldDataType dataType,
        string expectedStoreType)
    {
        // Arrange
        using var context = CreateContext();
        var query = context.Set<AuthUser>().AsQueryable();

        // Act
        var ordered = query.OrderByUserCustomField(
            new UserCustomFieldSort("metric", dataType),
            SortOrderConstants.Ascending);
        string sql = ordered.ToQueryString();

        // Assert
        sql.ShouldContain("jsonb_extract_path_text");
        sql.ShouldContain("\"CustomFields\"");
        sql.ShouldContain("pg_input_is_valid");
        sql.ShouldContain("ORDER BY");

        if (dataType == FieldDataType.Date)
        {
            sql.ShouldContain("timezone('UTC'");
            sql.ShouldContain("date");
        }
        else
        {
            sql.ShouldContain($"'{expectedStoreType}'");
        }

        if (dataType is FieldDataType.Number or FieldDataType.Date or FieldDataType.DateTime)
        {
            sql.ShouldContain("regexp_like");
            sql.ShouldContain("Infinity");
        }
    }

    [Fact]
    public void OrderByUserCustomFieldDescendingShouldSortNullsLastWithDeterministicIdTieBreaker()
    {
        // Arrange
        using var context = CreateContext();
        var query = context.Set<AuthUser>().AsQueryable();

        // Act
        var ordered = query.OrderByUserCustomField(
            new UserCustomFieldSort("score", FieldDataType.Number),
            SortOrderConstants.Descending);
        string sql = ordered.ToQueryString();

        // Assert
        sql.ShouldContain("jsonb_extract_path_text");
        sql.ShouldContain("IS NULL");
        sql.ShouldContain("DESC");
        sql.ShouldContain("\"Id\"");
    }

    [Fact]
    public void OrderByUserCustomFieldWhenTextShouldNotEmitSafeCast()
    {
        // Arrange
        using var context = CreateContext();
        var query = context.Set<AuthUser>().AsQueryable();

        // Act
        var ordered = query.OrderByUserCustomField(
            new UserCustomFieldSort("city", FieldDataType.Text),
            SortOrderConstants.Ascending);
        string sql = ordered.ToQueryString();

        // Assert
        sql.ShouldContain("jsonb_extract_path_text");
        sql.ShouldNotContain("pg_input_is_valid");
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
            publicKeys: ["city"],
            privateKeys: ["secret_note"],
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
            publicKeys: ["city", "salary"],
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
    public void UserCustomFieldSchemaAndUserJsonbColumnShouldBeConfiguredProperlyWithoutPendingModelChanges()
    {
        // Arrange
        using var context = CreateContext();
        var fieldEntity = context.Model.FindEntityType(typeof(UserCustomField))!;
        var dataTypeProperty = fieldEntity.FindProperty(nameof(UserCustomField.DataType))!;
        var userEntity = context.Model.FindEntityType(typeof(User))!;
        var customFieldsProperty = userEntity.FindProperty(nameof(User.CustomFields))!;

        // Assert
        dataTypeProperty.GetValueConverter().ShouldBeNull();
        dataTypeProperty.GetColumnType().ShouldBe("smallint");
        dataTypeProperty.IsNullable.ShouldBeFalse();

        customFieldsProperty.GetColumnType().ShouldBe("jsonb");
        customFieldsProperty.IsNullable.ShouldBeFalse();
        customFieldsProperty.GetDefaultValueSql().ShouldBe("'{}'::jsonb");

        context.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    [Fact]
    public void ReservedKeysShouldStayInSyncWithUserDtoAndIdentityUserProperties()
    {
        UserConstants.Email.ShouldBe(nameof(UserDto.Email));
        UserConstants.AvatarUrl.ShouldBe(nameof(UserDto.AvatarUrl));
        UserConstants.Roles.ShouldBe(nameof(UserDto.Roles));
        UserConstants.CustomFields.ShouldBe(nameof(UserDto.CustomFields));
        UserCustomFieldExtensions.SortPrefix.ShouldBe(nameof(UserDto.CustomFields), StringCompareShould.IgnoreCase);

        foreach (var prop in typeof(UserDto).GetProperties())
        {
            UserCustomFieldExtensions.ReservedKeys.ShouldContain(prop.Name);
        }

        foreach (var prop in typeof(IdentityUser<Guid>).GetProperties())
        {
            UserCustomFieldExtensions.ReservedKeys.ShouldContain(prop.Name);
        }
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;

        return new ApplicationDbContext(options);
    }
}
