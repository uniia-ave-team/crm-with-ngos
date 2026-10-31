using Crm.Application.Dtos.CustomTables;
using Crm.Application.Features.CustomTables.Extensions;
using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Shouldly;

namespace Crm.Application.Tests.Features.CustomTables.Extensions;

/// <summary>
/// Contains unit tests for <see cref="CustomTableSchemaExtensions.ComputeSchemaDiff"/>
/// validating field addition, deletion, key rename detection, backfill tracking, and data type validation.
/// </summary>
public class CustomTableSchemaExtensionsTests
{
    /// <summary>
    /// Verifies that removing existing fields and adding new fields are accurately categorized in the schema diff.
    /// </summary>
    [Fact]
    public void ComputeSchemaDiffShouldCategorizeAddedAndRemovedFields()
    {
        // Arrange
        var table = new CustomTable
        {
            Id = Guid.NewGuid(),
            Name = "Volunteers",
            Fields =
            [
                new CustomField { Id = Guid.NewGuid(), Key = "old_notes", Name = "Notes", DataType = FieldDataType.Text },
                new CustomField { Id = Guid.NewGuid(), Key = "email", Name = "Email", DataType = FieldDataType.Email },
            ],
        };

        var keptFieldId = table.Fields.ElementAt(1).Id;
        var incomingFields = new List<UpdateCustomFieldDto>
        {
            new(keptFieldId, "Email Address", "email", FieldDataType.Email, false, 0),
            new(null, "Birth Date", "birth_date", FieldDataType.Date, true, 1),
        };

        // Act
        var diff = table.ComputeSchemaDiff(incomingFields, hasExistingRecords: true);

        // Assert
        diff.FieldsToRemove.ShouldHaveSingleItem();
        diff.FieldsToRemove[0].Key.ShouldBe("old_notes");

        diff.FieldsToAdd.ShouldHaveSingleItem();
        diff.FieldsToAdd[0].Key.ShouldBe("birth_date");
        diff.FieldsToAdd[0].IsRequired.ShouldBeTrue();

        diff.KeyRenames.ShouldBeEmpty();
        diff.FieldsNeedingBackfill.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that single and chained key renames are detected and captured in <see cref="SchemaDiffResult.KeyRenames"/>.
    /// </summary>
    [Fact]
    public void ComputeSchemaDiffWhenFieldKeyIsRenamedShouldCaptureKeyRename()
    {
        // Arrange
        var fieldId = Guid.NewGuid();
        var table = new CustomTable
        {
            Id = Guid.NewGuid(),
            Name = "Projects",
            Fields =
            [
                new CustomField { Id = fieldId, Key = "proj_code", Name = "Code", DataType = FieldDataType.Text },
            ],
        };

        var incoming = new List<UpdateCustomFieldDto>
        {
            new(fieldId, "Project Code", "project_code", FieldDataType.Text, false, 0),
        };

        // Act
        var diff = table.ComputeSchemaDiff(incoming, hasExistingRecords: true);

        // Assert
        diff.KeyRenames.ShouldHaveSingleItem();
        diff.KeyRenames[0].FieldId.ShouldBe(fieldId);
        diff.KeyRenames[0].OldKey.ShouldBe("proj_code");
        diff.KeyRenames[0].NewKey.ShouldBe("project_code");
        diff.FieldsToRemove.ShouldBeEmpty();
        diff.FieldsToAdd.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that swapping keys between two existing fields is handled safely and produces rename entries for both.
    /// </summary>
    [Fact]
    public void ComputeSchemaDiffWhenKeysAreSwappedShouldCaptureBothRenames()
    {
        // Arrange
        var fieldAId = Guid.NewGuid();
        var fieldBId = Guid.NewGuid();

        var table = new CustomTable
        {
            Id = Guid.NewGuid(),
            Name = "Inventory",
            Fields =
            [
                new CustomField { Id = fieldAId, Key = "primary_code", Name = "Primary", DataType = FieldDataType.Text },
                new CustomField { Id = fieldBId, Key = "secondary_code", Name = "Secondary", DataType = FieldDataType.Text },
            ],
        };

        var incoming = new List<UpdateCustomFieldDto>
        {
            new(fieldAId, "Primary", "secondary_code", FieldDataType.Text, false, 0),
            new(fieldBId, "Secondary", "primary_code", FieldDataType.Text, false, 1),
        };

        // Act
        var diff = table.ComputeSchemaDiff(incoming, hasExistingRecords: true);

        // Assert
        diff.KeyRenames.Count.ShouldBe(2);
        diff.KeyRenames.ShouldContain(r => r.OldKey == "primary_code" && r.NewKey == "secondary_code");
        diff.KeyRenames.ShouldContain(r => r.OldKey == "secondary_code" && r.NewKey == "primary_code");
        diff.FieldsToRemove.ShouldBeEmpty();
        diff.FieldsToAdd.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that when an optional existing field becomes required, it is tracked in <see cref="SchemaDiffResult.FieldsNeedingBackfill"/>.
    /// </summary>
    [Fact]
    public void ComputeSchemaDiffWhenOptionalFieldBecomesRequiredShouldIdentifyBackfillRequirement()
    {
        // Arrange
        var fieldId = Guid.NewGuid();
        var table = new CustomTable
        {
            Id = Guid.NewGuid(),
            Name = "Beneficiaries",
            Fields =
            [
                new CustomField { Id = fieldId, Key = "tax_id", Name = "Tax ID", DataType = FieldDataType.Text, IsRequired = false },
            ],
        };

        var incoming = new List<UpdateCustomFieldDto>
        {
            new(fieldId, "Tax ID", "tax_id", FieldDataType.Text, true, 0),
        };

        // Act
        var diff = table.ComputeSchemaDiff(incoming, hasExistingRecords: true);

        // Assert
        diff.FieldsNeedingBackfill.ShouldHaveSingleItem();
        diff.FieldsNeedingBackfill[0].Key.ShouldBe("tax_id");
    }

    /// <summary>
    /// Verifies that already required fields, or fields changing from required to optional,
    /// do not trigger a backfill requirement.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void ComputeSchemaDiffWhenFieldRequirementDoesNotTransitionToRequiredShouldNotBackfill(bool initialRequired, bool updatedRequired)
    {
        // Arrange
        var fieldId = Guid.NewGuid();
        var table = new CustomTable
        {
            Id = Guid.NewGuid(),
            Name = "Events",
            Fields =
            [
                new CustomField { Id = fieldId, Key = "location", Name = "Location", DataType = FieldDataType.Text, IsRequired = initialRequired },
            ],
        };

        var incoming = new List<UpdateCustomFieldDto>
        {
            new(fieldId, "Location", "location", FieldDataType.Text, updatedRequired, 0),
        };

        // Act
        var diff = table.ComputeSchemaDiff(incoming, hasExistingRecords: true);

        // Assert
        diff.FieldsNeedingBackfill.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that attempting to change a field's data type when the table contains existing records
    /// throws a <see cref="FieldDataTypeChangeNotAllowedException"/>.
    /// </summary>
    [Fact]
    public void ComputeSchemaDiffWhenFieldDataTypeChangesWithExistingRecordsShouldThrowException()
    {
        // Arrange
        var fieldId = Guid.NewGuid();
        var table = new CustomTable
        {
            Id = Guid.NewGuid(),
            Name = "Donations",
            Fields =
            [
                new CustomField { Id = fieldId, Key = "amount", Name = "Amount", DataType = FieldDataType.Number },
            ],
        };

        var incoming = new List<UpdateCustomFieldDto>
        {
            new(fieldId, "Amount", "amount", FieldDataType.Text, false, 0),
        };

        // Act & Assert
        var exception = Should.Throw<FieldDataTypeChangeNotAllowedException>(() =>
            table.ComputeSchemaDiff(incoming, hasExistingRecords: true));

        exception.Message.ShouldContain("amount");
    }

    /// <summary>
    /// Verifies that changing a field's data type when the table does NOT contain records is allowed.
    /// </summary>
    [Fact]
    public void ComputeSchemaDiffWhenFieldDataTypeChangesWithoutExistingRecordsShouldSucceed()
    {
        // Arrange
        var fieldId = Guid.NewGuid();
        var table = new CustomTable
        {
            Id = Guid.NewGuid(),
            Name = "EmptyTable",
            Fields =
            [
                new CustomField { Id = fieldId, Key = "counter", Name = "Counter", DataType = FieldDataType.Number },
            ],
        };

        var incoming = new List<UpdateCustomFieldDto>
        {
            new(fieldId, "Counter", "counter", FieldDataType.Text, false, 0),
        };

        // Act
        var diff = table.ComputeSchemaDiff(incoming, hasExistingRecords: false);

        // Assert
        diff.FieldsToRemove.ShouldBeEmpty();
        diff.FieldsToAdd.ShouldBeEmpty();
        table.Fields.Single(f => f.Id == fieldId).DataType.ShouldBe(FieldDataType.Text);
    }
}
