using Crm.Application.Dtos.CustomTables;
using Crm.Application.Extensions;
using Crm.Application.Features.CustomTables.Mappings;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Mapster;
using Shouldly;

namespace Crm.Application.Tests.Features.CustomTables.Extensions;

/// <summary>
/// Contains unit tests for <see cref="FieldSchemaSynchronizer"/> verifying ID-based matching,
/// immutable field ID preservation, JSONB sync plan computation, and zero-mutation validation.
/// </summary>
public class FieldSchemaSynchronizerTests
{
    static FieldSchemaSynchronizerTests()
    {
        new CustomTableMappingRegister().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public void SynchronizeFieldsWhenDtoHasValidIdShouldMatchByIdAndPreserveId()
    {
        // Arrange
        var fieldId = Guid.CreateVersion7();
        var existing = new CustomField
        {
            Id = fieldId,
            Name = "First Name",
            IsRequired = false,
            Order = 0,
        };
        List<CustomField> fields = [existing];

        var dto = new UpdateCustomFieldDto(fieldId, "Updated First Name", IsRequired: false, Order: 0);

        // Act
        var plan = fields.SynchronizeFields([dto]);

        // Assert
        fields.ShouldHaveSingleItem().ShouldBeSameAs(existing);
        existing.Id.ShouldBe(fieldId);
        existing.Name.ShouldBe("Updated First Name");
        plan.AddedFields.ShouldBeEmpty();
        plan.RemovedFields.ShouldBeEmpty();
        plan.KeysToDrop.ShouldBeEmpty();
        plan.KeysToBackfill.ShouldBeEmpty();
        plan.RequiresJsonbSync.ShouldBeFalse();
    }

    [Fact]
    public void SynchronizeFieldsWhenDtoHasNullIdShouldGenerateGuidIdAndTrackBackfillOnlyIfRequired()
    {
        // Arrange
        List<CustomField> fields = [];

        UpdateCustomFieldDto[] incoming =
        [
            new(null, "Required Column", IsRequired: true, Order: 0),
            new(null, "Optional Column", IsRequired: false, Order: 1),
        ];

        // Act
        var plan = fields.SynchronizeFields(incoming);

        // Assert
        fields.Count.ShouldBe(2);
        plan.AddedFields.Count.ShouldBe(2);
        plan.RemovedFields.ShouldBeEmpty();
        plan.KeysToDrop.ShouldBeEmpty();

        var requiredField = fields.Single(f => f.Name == "Required Column");
        var optionalField = fields.Single(f => f.Name == "Optional Column");

        requiredField.Id.ShouldNotBe(Guid.Empty);
        optionalField.Id.ShouldNotBe(Guid.Empty);
        requiredField.Id.ShouldNotBe(optionalField.Id);

        plan.KeysToBackfill.ShouldBe([requiredField.Id.ToString()]);
        plan.RequiresJsonbSync.ShouldBeTrue();
    }

    [Fact]
    public void SynchronizeFieldsWhenOptionalFieldBecomesRequiredAndStaleFieldRemovedShouldPopulateDropAndBackfillKeys()
    {
        // Arrange
        var keepId = Guid.CreateVersion7();
        var staleId = Guid.CreateVersion7();

        var keptEntity = new CustomField
        {
            Id = keepId,
            Name = "Keep Me",
            IsRequired = false,
            Order = 0,
        };
        var staleEntity = new CustomField
        {
            Id = staleId,
            Name = "Stale",
            IsRequired = false,
            Order = 1,
        };
        List<CustomField> fields = [keptEntity, staleEntity];

        // Act
        var plan = fields.SynchronizeFields(
        [
            new UpdateCustomFieldDto(keepId, "Now Mandatory", IsRequired: true, Order: 0),
            new UpdateCustomFieldDto(null, "Brand New", IsRequired: true, Order: 1),
        ]);

        // Assert
        fields.ShouldNotContain(staleEntity);
        fields.Count.ShouldBe(2);

        keptEntity.Name.ShouldBe("Now Mandatory");
        keptEntity.IsRequired.ShouldBeTrue();
        keptEntity.Id.ShouldBe(keepId);

        var added = fields.Single(f => f.Name == "Brand New");
        plan.AddedFields.ShouldBe([added]);
        plan.RemovedFields.ShouldBe([staleEntity]);
        plan.KeysToDrop.ShouldBe([staleId.ToString()]);
        plan.KeysToBackfill.ShouldBe([added.Id.ToString(), keepId.ToString()], ignoreOrder: true);
        plan.RequiresJsonbSync.ShouldBeTrue();
    }

    [Fact]
    public void SynchronizeFieldsWhenIncomingDtosContainDuplicateIdsShouldThrowWithoutMutatingExistingFields()
    {
        // Arrange
        var duplicateId = Guid.CreateVersion7();
        var existingField = new CustomField
        {
            Id = duplicateId,
            Name = "Original Name",
        };
        List<CustomField> fields = [existingField];

        // Act & Assert
        var exception = Should.Throw<DomainValidationException>(() =>
            fields.SynchronizeFields(
            [
                new UpdateCustomFieldDto(duplicateId, "First", false, 0),
                new UpdateCustomFieldDto(duplicateId, "Second", false, 1),
            ]));

        exception.Message.ShouldContain(duplicateId.ToString());
        existingField.Name.ShouldBe("Original Name");
    }

    [Fact]
    public void SynchronizeFieldsWhenIncomingDtoHasNonExistentIdShouldThrowNotFoundWithoutMutatingExistingFields()
    {
        // Arrange
        var existingField = new CustomField
        {
            Id = Guid.CreateVersion7(),
            Name = "Original",
        };
        List<CustomField> fields = [existingField];

        // Act & Assert
        Should.Throw<EntityNotFoundException<CustomField>>(() =>
            fields.SynchronizeFields(
            [
                new UpdateCustomFieldDto(existingField.Id, "Mutated", false, 0),
                new UpdateCustomFieldDto(Guid.CreateVersion7(), "Unknown", false, 1),
            ]));

        existingField.Name.ShouldBe("Original");
    }
}
