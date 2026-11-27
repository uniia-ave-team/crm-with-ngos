using System.Text.Json.Nodes;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Shouldly;

namespace Crm.Domain.Tests.Extensions;

/// <summary>
/// Unit tests verifying JSONB string-only payload validation/backfilling, default payload initialization,
/// field-ID sort key validation, schema partitioning, and ordering resolution for user custom fields.
/// </summary>
public class UserCustomFieldDomainTests
{
    [Fact]
    public void ValidateAndBackfillPayloadForUserCustomFieldsShouldValidateStringsBackfillOptionalAndRejectInvalidPayloads()
    {
        // Arrange
        var ageField = new UserCustomField { Name = "Age", IsRequired = true, IsPublic = true, Order = 0 };
        var cityField = new UserCustomField { Name = "City", IsRequired = false, IsPublic = true, Order = 1 };
        var activeField = new UserCustomField { Name = "Active", IsRequired = false, IsPublic = false, Order = 2 };
        var schema = new List<UserCustomField> { ageField, cityField, activeField };

        string ageKey = ageField.Id.ToString();
        string cityKey = cityField.Id.ToString();
        string activeKey = activeField.Id.ToString();

        var validPayload = new JsonObject
        {
            [ageKey] = "28",
            [activeKey] = "true",
        };

        // Act
        var aligned = validPayload.ValidateAndBackfillPayload(schema);

        // Assert
        aligned.Select(kvp => kvp.Key).ShouldBe([ageKey, cityKey, activeKey]);
        aligned[ageKey]?.GetValue<string>().ShouldBe("28");
        aligned[cityKey].ShouldBeNull();
        aligned[activeKey]?.GetValue<string>().ShouldBe("true");

        // Non-string JSON values (number, boolean, object, array) are rejected
        Should.Throw<InvalidCustomRecordPayloadException>(() =>
            new JsonObject { [ageKey] = 28 }.ValidateAndBackfillPayload(schema));
        Should.Throw<InvalidCustomRecordPayloadException>(() =>
            new JsonObject { [ageKey] = "28", [activeKey] = true }.ValidateAndBackfillPayload(schema));
        Should.Throw<InvalidCustomRecordPayloadException>(() =>
            new JsonObject { [ageKey] = "28", [cityKey] = new JsonObject() }.ValidateAndBackfillPayload(schema));
        Should.Throw<InvalidCustomRecordPayloadException>(() =>
            new JsonObject { [ageKey] = "28", [cityKey] = new JsonArray("a") }.ValidateAndBackfillPayload(schema));

        // Missing or null required field is rejected
        Should.Throw<InvalidCustomRecordPayloadException>(() =>
            new JsonObject { [cityKey] = "Kyiv" }.ValidateAndBackfillPayload(schema));
        Should.Throw<InvalidCustomRecordPayloadException>(() =>
            new JsonObject { [ageKey] = null }.ValidateAndBackfillPayload(schema));

        // Unknown field key is rejected
        Should.Throw<InvalidCustomRecordPayloadException>(() =>
            new JsonObject { [ageKey] = "28", [Guid.CreateVersion7().ToString()] = "oops" }.ValidateAndBackfillPayload(schema));
    }

    [Fact]
    public void CreateDefaultPayloadShouldBackfillRequiredFieldsWithEmptyStringAndSetOptionalFieldsToNullInOrder()
    {
        // Arrange
        var notesField = new UserCustomField { Name = "Notes", IsRequired = false, Order = 2 };
        var yearsField = new UserCustomField { Name = "Years", IsRequired = true, Order = 0 };
        var verifiedField = new UserCustomField { Name = "Verified", IsRequired = true, Order = 1 };
        var schema = new List<UserCustomField> { notesField, yearsField, verifiedField };

        // Act
        var payload = schema.CreateDefaultPayload();

        // Assert
        payload.Select(kvp => kvp.Key).ShouldBe([yearsField.Id.ToString(), verifiedField.Id.ToString(), notesField.Id.ToString()]);
        payload[yearsField.Id.ToString()]?.GetValue<string>().ShouldBe(string.Empty);
        payload[verifiedField.Id.ToString()]?.GetValue<string>().ShouldBe(string.Empty);
        payload[notesField.Id.ToString()].ShouldBeNull();
    }

    [Fact]
    public void PartitionSchemaKeysShouldSplitAllPublicAndPrivateFieldIdsPreservingOrder()
    {
        // Arrange
        var cityField = new UserCustomField { Name = "City", IsPublic = true, Order = 0 };
        var salaryField = new UserCustomField { Name = "Salary", IsPublic = false, Order = 1 };
        var roleNoteField = new UserCustomField { Name = "Role Note", IsPublic = true, Order = 2 };
        var schema = new List<UserCustomField> { cityField, salaryField, roleNoteField };

        // Act
        var (allKeys, publicKeys, privateKeys) = schema.PartitionSchemaKeys();

        // Assert
        allKeys.ShouldBe([cityField.Id.ToString(), salaryField.Id.ToString(), roleNoteField.Id.ToString()]);
        publicKeys.ShouldBe([cityField.Id.ToString(), roleNoteField.Id.ToString()]);
        privateKeys.ShouldBe([salaryField.Id.ToString()]);
    }

    [Theory]
    [InlineData("019a0000-0000-7000-8000-000000000001", true)]
    [InlineData("019A0000-0000-7000-8000-000000000001", true)]
    [InlineData(nameof(User.FirstName), true)]
    [InlineData(UserConstants.Email, true)]
    [InlineData(UserConstants.FullName, true)]
    [InlineData("00000000-0000-0000-0000-000000000000", false)]
    [InlineData("city", false)]
    [InlineData("f_019a0000000000000000000000000001", false)]
    [InlineData("PasswordHash", false)]
    [InlineData("SecurityStamp", false)]
    [InlineData("ConcurrencyStamp", false)]
    [InlineData("customFields.city", false)]
    [InlineData("customFields['birth_date']", false)]
    [InlineData("bad key", false)]
    [InlineData("key; DROP TABLE Users;", false)]
    public void IsAllowedUserOrderByShouldAcceptBuiltInFieldsAndNonEmptyFieldGuidsOnly(string orderBy, bool expected)
    {
        UserCustomFieldExtensions.IsAllowedUserOrderBy(orderBy).ShouldBe(expected);
    }

    [Fact]
    public void ResolveOrderingWhenMatchingSchemaFieldExistsShouldResolveCanonicalLowercaseGuidAndRejectUnknown()
    {
        // Arrange
        var birthDateField = new UserCustomField { Name = "Birth Date", IsPublic = true, Order = 0 };
        var ageField = new UserCustomField { Name = "Age", IsPublic = true, Order = 1 };
        var fields = new[] { birthDateField, ageField };

        // Act
        var resolvedUpper = fields.ResolveOrdering(birthDateField.Id.ToString().ToUpperInvariant());
        var resolvedExact = fields.ResolveOrdering(ageField.Id.ToString());
        var rejectedPrefixed = fields.ResolveOrdering($"customFields['{ageField.Id}']");
        var missing = fields.ResolveOrdering(Guid.CreateVersion7().ToString());

        // Assert
        resolvedUpper.ShouldBe(birthDateField.Id.ToString());
        resolvedExact.ShouldBe(ageField.Id.ToString());
        rejectedPrefixed.ShouldBeNull();
        missing.ShouldBeNull();
    }
}
