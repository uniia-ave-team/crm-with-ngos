using System.Linq.Expressions;
using System.Text.Json.Nodes;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Infrastructure.Persistence.Extensions.FunctionExtensions;
using Shouldly;

namespace Crm.Infrastructure.Tests.Features.CustomTables;

/// <summary>
/// Contains unit tests for <see cref="JsonbFunctionsExtensions"/> verifying schema patch creation,
/// backfill mapping, linear growth, and EF Core update lambda expression generation.
/// </summary>
public class JsonbFunctionsExtensionsTests
{
    private static readonly Expression<Func<CustomRecord, JsonObject>> PropertySelector = r => r.Data;

    /// <summary>
    /// Verifies that when a diff contains no changes, <see cref="JsonbFunctionsExtensions.BuildSchemaPatch"/>
    /// generates empty arrays and <see cref="JsonbFunctionsExtensions.ToSchemaUpdateLambda{TEntity}"/> returns <see langword="null"/>.
    /// </summary>
    [Fact]
    public void BuildSchemaPatchWhenDiffIsEmptyShouldReturnEmptyArraysAndNullLambda()
    {
        // Arrange
        var emptyDiff = new SchemaDiffResult([], [], [], []);

        // Act
        var patch = JsonbFunctionsExtensions.BuildSchemaPatch(emptyDiff);
        var lambda = PropertySelector.ToSchemaUpdateLambda(emptyDiff);

        // Assert
        patch.DropKeys.ShouldBeEmpty();
        patch.TargetKeys.ShouldBeEmpty();
        patch.SourceKeys.ShouldBeEmpty();
        patch.FallbackJson.ShouldBeEmpty();
        lambda.ShouldBeNull();
    }

    /// <summary>
    /// Verifies that dropped keys include both explicitly removed fields and old keys from key renames.
    /// </summary>
    [Fact]
    public void BuildSchemaPatchShouldIncludeRemovedFieldsAndOldKeysInDropKeys()
    {
        // Arrange
        var removedField = new CustomField { Key = "obsolete_key" };
        var rename = new KeyRename(Guid.NewGuid(), "old_name", "new_name");

        var diff = new SchemaDiffResult([removedField], [], [rename], []);

        // Act
        var patch = JsonbFunctionsExtensions.BuildSchemaPatch(diff);

        // Assert
        patch.DropKeys.Length.ShouldBe(2);
        patch.DropKeys.ShouldContain("obsolete_key");
        patch.DropKeys.ShouldContain("old_name");

        patch.TargetKeys.ShouldHaveSingleItem();
        patch.TargetKeys[0].ShouldBe("new_name");
        patch.SourceKeys[0].ShouldBe("old_name");
        patch.FallbackJson[0].ShouldBe(string.Empty);
    }

    /// <summary>
    /// Verifies that when a renamed field also requires a backfill, the patch correctly maps the target key,
    /// sources from the old key, and provides the fallback JSON value.
    /// </summary>
    [Fact]
    public void BuildSchemaPatchWhenRenamedFieldRequiresBackfillShouldMapSourceKeyToOldKeyWithFallback()
    {
        // Arrange
        var fieldId = Guid.NewGuid();
        var rename = new KeyRename(fieldId, "old_status", "new_status");
        var backfillField = new CustomField { Id = fieldId, Key = "new_status", DataType = FieldDataType.Text, IsRequired = true };

        var diff = new SchemaDiffResult([], [], [rename], [backfillField]);

        // Act
        var patch = JsonbFunctionsExtensions.BuildSchemaPatch(diff);

        // Assert
        patch.DropKeys.ShouldHaveSingleItem();
        patch.DropKeys[0].ShouldBe("old_status");

        patch.TargetKeys.ShouldHaveSingleItem();
        patch.TargetKeys[0].ShouldBe("new_status");
        patch.SourceKeys[0].ShouldBe("old_status");
        patch.FallbackJson[0].ShouldBe("\"\"");
    }

    /// <summary>
    /// Verifies that new required fields receive an empty source key and a fallback JSON literal,
    /// while optional new fields are completely omitted from the schema patch payload.
    /// </summary>
    [Fact]
    public void BuildSchemaPatchWhenFieldsAddedShouldIncludeOnlyRequiredFieldsWithEmptySource()
    {
        // Arrange
        var requiredAdded = new CustomField { Key = "mandatory_code", DataType = FieldDataType.Number, IsRequired = true };
        var optionalAdded = new CustomField { Key = "optional_notes", DataType = FieldDataType.Text, IsRequired = false };

        var diff = new SchemaDiffResult([], [requiredAdded, optionalAdded], [], []);

        // Act
        var patch = JsonbFunctionsExtensions.BuildSchemaPatch(diff);

        // Assert
        patch.DropKeys.ShouldBeEmpty();
        patch.TargetKeys.ShouldHaveSingleItem();
        patch.TargetKeys[0].ShouldBe("mandatory_code");
        patch.SourceKeys[0].ShouldBe(string.Empty);
        patch.FallbackJson[0].ShouldBe("0");
    }

    /// <summary>
    /// Regression test verifying that adding 30 fields constructs the flat patch linearly
    /// with matching array dimensions and without exponential complexity.
    /// </summary>
    [Fact]
    public void BuildSchemaPatchWhenThirtyFieldsAddedShouldScaleLinearly()
    {
        // Arrange
        const int count = 30;
        var addedFields = Enumerable.Range(0, count)
            .Select(i => new CustomField
            {
                Key = $"field_{i}",
                DataType = FieldDataType.Text,
                IsRequired = true,
            })
            .ToList();

        var diff = new SchemaDiffResult([], addedFields, [], []);

        // Act
        var patch = JsonbFunctionsExtensions.BuildSchemaPatch(diff);
        var lambda = PropertySelector.ToSchemaUpdateLambda(diff);

        // Assert
        patch.TargetKeys.Length.ShouldBe(count);
        patch.SourceKeys.Length.ShouldBe(count);
        patch.FallbackJson.Length.ShouldBe(count);
        lambda.ShouldNotBeNull();
    }
}
