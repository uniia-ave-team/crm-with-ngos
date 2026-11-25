using System.Text.Json.Nodes;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Shouldly;

namespace Crm.Domain.Tests.Extensions;

/// <summary>
/// Unit tests verifying JSONB payload validation/backfilling, default payload initialization,
/// sort key extraction (bare and prefixed), reserved key checks, schema partitioning, and ordering resolution
/// for user custom fields.
/// </summary>
public class UserCustomFieldDomainTests
{
    [Fact]
    public void ValidateAndBackfillPayloadForUserCustomFieldsShouldValidateTypesBackfillOptionalAndRejectUnknownKeys()
    {
        // Arrange
        var schema = new List<UserCustomField>
        {
            new() { Name = "Age", Key = "age", DataType = FieldDataType.Number, IsRequired = true, IsPublic = true, Order = 0 },
            new() { Name = "City", Key = "city", DataType = FieldDataType.Text, IsRequired = false, IsPublic = true, Order = 1 },
            new() { Name = "Active", Key = "is_volunteer", DataType = FieldDataType.Boolean, IsRequired = false, IsPublic = false, Order = 2 },
        };

        var validPayload = new JsonObject
        {
            ["age"] = 28,
            ["is_volunteer"] = true,
        };

        // Act
        var aligned = validPayload.ValidateAndBackfillPayload(schema);

        // Assert
        aligned.Select(kvp => kvp.Key).ShouldBe(["age", "city", "is_volunteer"]);
        aligned["age"]?.GetValue<int>().ShouldBe(28);
        aligned["city"].ShouldBeNull();
        aligned["is_volunteer"]?.GetValue<bool>().ShouldBeTrue();

        Should.Throw<InvalidCustomRecordPayloadException>(() =>
            new JsonObject { ["age"] = "not-a-number" }.ValidateAndBackfillPayload(schema));

        Should.Throw<InvalidCustomRecordPayloadException>(() =>
            new JsonObject { ["city"] = "Kyiv" }.ValidateAndBackfillPayload(schema));

        Should.Throw<InvalidCustomRecordPayloadException>(() =>
            new JsonObject { ["age"] = 28, ["unknown_key"] = "oops" }.ValidateAndBackfillPayload(schema));
    }

    [Fact]
    public void CreateDefaultPayloadShouldBackfillRequiredFieldsAndSetOptionalFieldsToNullInOrder()
    {
        // Arrange
        var schema = new List<UserCustomField>
        {
            new() { Name = "Notes", Key = "notes", DataType = FieldDataType.Text, IsRequired = false, Order = 2 },
            new() { Name = "Years", Key = "years", DataType = FieldDataType.Number, IsRequired = true, Order = 0 },
            new() { Name = "Verified", Key = "verified", DataType = FieldDataType.Boolean, IsRequired = true, Order = 1 },
        };

        // Act
        var payload = schema.CreateDefaultPayload();

        // Assert
        payload.Select(kvp => kvp.Key).ShouldBe(["years", "verified", "notes"]);
        payload["years"]?.GetValue<decimal>().ShouldBe(0m);
        payload["verified"]?.GetValue<bool>().ShouldBeFalse();
        payload["notes"].ShouldBeNull();
    }

    [Fact]
    public void PartitionSchemaKeysShouldSplitAllPublicAndPrivateKeysPreservingOrder()
    {
        // Arrange
        var schema = new List<UserCustomField>
        {
            new() { Name = "City", Key = "city", DataType = FieldDataType.Text, IsPublic = true, Order = 0 },
            new() { Name = "Salary", Key = "salary", DataType = FieldDataType.Number, IsPublic = false, Order = 1 },
            new() { Name = "Role Note", Key = "role_note", DataType = FieldDataType.Text, IsPublic = true, Order = 2 },
        };

        // Act
        var (allKeys, publicKeys, privateKeys) = schema.PartitionSchemaKeys();

        // Assert
        allKeys.ShouldBe(["city", "salary", "role_note"]);
        publicKeys.ShouldBe(["city", "role_note"]);
        privateKeys.ShouldBe(["salary"]);
    }

    [Theory]
    [InlineData("city", "city")]
    [InlineData("birth_date", "birth_date")]
    [InlineData("t_shirt_size", "t_shirt_size")]
    [InlineData("customFields.city", "city")]
    [InlineData("customFields.birth_date", "birth_date")]
    [InlineData("customFields['birth_date']", "birth_date")]
    [InlineData("customFields[\"t_shirt_size\"]", "t_shirt_size")]
    [InlineData("CUSTOMFIELDS [ 'age' ]", "age")]
    public void ExtractSortKeyWhenValidBareOrPrefixedExpressionShouldReturnKey(string orderBy, string expectedKey)
    {
        // Act
        string? extracted = UserCustomFieldExtensions.ExtractSortKey(orderBy);

        // Assert
        extracted.ShouldBe(expectedKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(nameof(User.FirstName))]
    [InlineData(UserConstants.Email)]
    [InlineData(UserConstants.FullName)]
    [InlineData("PasswordHash")]
    [InlineData("SecurityStamp")]
    [InlineData("ConcurrencyStamp")]
    [InlineData($"{UserCustomFieldExtensions.SortPrefix}.")]
    [InlineData($"{UserCustomFieldExtensions.SortPrefix}.{nameof(User.FirstName)}")]
    [InlineData($"{UserCustomFieldExtensions.SortPrefix}.PasswordHash")]
    [InlineData($"{UserCustomFieldExtensions.SortPrefix}.key; DROP TABLE Users;")]
    [InlineData($"{UserCustomFieldExtensions.SortPrefix}.nested.key")]
    [InlineData("bad key")]
    public void ExtractSortKeyWhenInvalidOrReservedShouldReturnNull(string? orderBy)
    {
        // Act
        string? extracted = UserCustomFieldExtensions.ExtractSortKey(orderBy);

        // Assert
        extracted.ShouldBeNull();
    }

    [Fact]
    public void ResolveOrderingWhenMatchingSchemaFieldExistsShouldResolveBothBareAndPrefixedKeys()
    {
        // Arrange
        var fields = new[]
        {
            new UserCustomField { Name = "Birth Date", Key = "birth_date", DataType = FieldDataType.Date, IsPublic = true, Order = 0 },
            new UserCustomField { Name = "Age", Key = "age", DataType = FieldDataType.Number, IsPublic = true, Order = 1 },
        };

        // Act
        var resolvedPrefixed = fields.ResolveOrdering("customFields['BIRTH_DATE']");
        var resolvedBare = fields.ResolveOrdering("age");
        var missing = fields.ResolveOrdering("unknown_col");

        // Assert
        resolvedPrefixed.ShouldNotBeNull();
        resolvedPrefixed.Key.ShouldBe("birth_date");
        resolvedPrefixed.DataType.ShouldBe(FieldDataType.Date);

        resolvedBare.ShouldNotBeNull();
        resolvedBare.Key.ShouldBe("age");
        resolvedBare.DataType.ShouldBe(FieldDataType.Number);

        missing.ShouldBeNull();
    }
}
