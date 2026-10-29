using Crm.Application.Dtos.CustomTables;
using Crm.Application.Extensions;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Shouldly;

namespace Crm.Application.Tests.Features.CustomTables.Extensions;

/// <summary>
/// Contains unit tests for <see cref="FieldSchemaSynchronizer"/> validating reconciliation and synchronization workflows.
/// </summary>
public class FieldSchemaSynchronizerTests
{
    /// <summary>
    /// Verifies that incoming DTOs containing valid entity identifiers match existing fields by ID.
    /// </summary>
    [Fact]
    public void ReconcileWhenDtoHasValidIdShouldMatchById()
    {
        // Arrange
        var fieldId = Guid.NewGuid();
        var existing = new CustomField
        {
            Id = fieldId,
            Key = "first_name",
            Name = "First Name",
            DataType = FieldDataType.Text,
        };

        var dto = new UpdateCustomFieldDto(
            fieldId,
            "Updated First Name",
            "first_name_renamed",
            FieldDataType.Text,
            false,
            0);

        // Act
        var result = FieldSchemaSynchronizer.Reconcile(
            [existing],
            [dto],
            id => new EntityNotFoundException<CustomField>(id),
            createEntity: d => new CustomField { Key = d.Key, Name = d.Name },
            updateEntity: (e, d) =>
            {
                e.Key = d.Key;
                e.Name = d.Name;
            });

        // Assert
        result.Matched.ShouldHaveSingleItem();
        result.Matched[0].ShouldBeSameAs(existing);
        result.Matched[0].Id.ShouldBe(fieldId);
        result.Matched[0].Name.ShouldBe("Updated First Name");
        result.Matched[0].Key.ShouldBe("first_name_renamed");
        result.Added.ShouldBeEmpty();
        result.Removed.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that an incoming DTO without an identifier matches an unclaimed existing entity by its key.
    /// </summary>
    [Fact]
    public void ReconcileWhenDtoHasNoIdShouldMatchByUnclaimedKey()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var existing = new CustomField
        {
            Id = existingId,
            Key = "phone_number",
            Name = "Phone",
            DataType = FieldDataType.PhoneNumber,
        };

        var dto = new UpdateCustomFieldDto(
            null,
            "Mobile Phone",
            "phone_number",
            FieldDataType.PhoneNumber,
            true,
            1);

        // Act
        var result = FieldSchemaSynchronizer.Reconcile(
            [existing],
            [dto],
            id => new EntityNotFoundException<CustomField>(id),
            createEntity: d => new CustomField { Key = d.Key, Name = d.Name },
            updateEntity: (e, d) =>
            {
                e.Name = d.Name;
                e.IsRequired = d.IsRequired;
            });

        // Assert
        result.Matched.ShouldHaveSingleItem();
        result.Matched[0].ShouldBeSameAs(existing);
        result.Matched[0].Id.ShouldBe(existingId);
        result.Matched[0].Name.ShouldBe("Mobile Phone");
        result.Matched[0].IsRequired.ShouldBeTrue();
        result.Added.ShouldBeEmpty();
        result.Removed.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that when an existing field is claimed by ID and renamed, a new incoming DTO without an ID
    /// that uses the old key is treated as an addition rather than hijacking the existing field.
    /// </summary>
    [Fact]
    public void ReconcileWhenExistingFieldClaimedAndNewDtoUsesOldKeyShouldTreatAsAddition()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var existingField = new CustomField
        {
            Id = existingId,
            Key = "legacy_code",
            Name = "Legacy Code",
            DataType = FieldDataType.Text,
        };

        var renameDto = new UpdateCustomFieldDto(
            existingId,
            "Modern Code",
            "modern_code",
            FieldDataType.Text,
            false,
            0);

        var newDtoWithOldKey = new UpdateCustomFieldDto(
            null,
            "Brand New Legacy",
            "legacy_code",
            FieldDataType.Text,
            false,
            1);

        // Act
        var result = FieldSchemaSynchronizer.Reconcile(
            [existingField],
            [renameDto, newDtoWithOldKey],
            id => new EntityNotFoundException<CustomField>(id),
            createEntity: d => new CustomField { Key = d.Key, Name = d.Name },
            updateEntity: (e, d) =>
            {
                e.Key = d.Key;
                e.Name = d.Name;
            });

        // Assert
        result.Matched.ShouldHaveSingleItem();
        result.Matched[0].Id.ShouldBe(existingId);
        result.Matched[0].Key.ShouldBe("modern_code");

        result.Added.ShouldHaveSingleItem();
        result.Added[0].Key.ShouldBe("legacy_code");
        result.Added[0].Name.ShouldBe("Brand New Legacy");
        result.Removed.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies that duplicate field identifiers in incoming DTOs cause an <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void ReconcileWhenIncomingDtosContainDuplicateIdsShouldThrowInvalidOperationException()
    {
        // Arrange
        var duplicateId = Guid.NewGuid();
        var existingField = new CustomField { Id = duplicateId, Key = "unique_key" };

        var dtoOne = new UpdateCustomFieldDto(duplicateId, "First", "first", FieldDataType.Text, false, 0);
        var dtoTwo = new UpdateCustomFieldDto(duplicateId, "Second", "second", FieldDataType.Text, false, 1);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            FieldSchemaSynchronizer.Reconcile(
                [existingField],
                [dtoOne, dtoTwo],
                id => new EntityNotFoundException<CustomField>(id),
                createEntity: d => new CustomField(),
                updateEntity: (e, d) => { }));

        exception.Message.ShouldContain(duplicateId.ToString());
    }

    /// <summary>
    /// Verifies that an incoming DTO referencing an unknown identifier invokes the not-found factory.
    /// </summary>
    [Fact]
    public void ReconcileWhenIncomingDtoHasNonExistentIdShouldThrowNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var existingField = new CustomField { Id = Guid.NewGuid(), Key = "field_one" };
        var incomingDto = new UpdateCustomFieldDto(nonExistentId, "Unknown", "unknown", FieldDataType.Text, false, 0);

        // Act & Assert
        Should.Throw<EntityNotFoundException<CustomField>>(() =>
            FieldSchemaSynchronizer.Reconcile(
                [existingField],
                [incomingDto],
                id => new EntityNotFoundException<CustomField>(id),
                createEntity: d => new CustomField(),
                updateEntity: (e, d) => { }));
    }

    /// <summary>
    /// Verifies the two-phase reconciliation guarantee: if validation fails during <c>onMatched</c>,
    /// no entities are mutated by <c>updateEntity</c> (zero partial mutations).
    /// </summary>
    [Fact]
    public void ReconcileWhenValidationThrowsInOnMatchedShouldGuaranteeZeroMutations()
    {
        // Arrange
        var fieldOneId = Guid.NewGuid();
        var fieldTwoId = Guid.NewGuid();

        var fieldOne = new CustomField { Id = fieldOneId, Key = "field_one", Name = "Original One" };
        var fieldTwo = new CustomField { Id = fieldTwoId, Key = "field_two", Name = "Original Two" };

        var dtoOne = new UpdateCustomFieldDto(fieldOneId, "Mutated One", "field_one", FieldDataType.Text, false, 0);
        var dtoTwo = new UpdateCustomFieldDto(fieldTwoId, "Mutated Two", "field_two", FieldDataType.Text, false, 1);

        // Act
        var act = () => FieldSchemaSynchronizer.Reconcile(
            [fieldOne, fieldTwo],
            [dtoOne, dtoTwo],
            id => new EntityNotFoundException<CustomField>(id),
            createEntity: d => new CustomField(),
            updateEntity: (e, d) => e.Name = d.Name,
            onMatched: (e, d) =>
            {
                if (e.Key == "field_two")
                {
                    throw new InvalidOperationException("Validation failed on second field.");
                }
            });

        // Assert
        Should.Throw<InvalidOperationException>(act);
        fieldOne.Name.ShouldBe("Original One");
        fieldTwo.Name.ShouldBe("Original Two");
    }

    /// <summary>
    /// Verifies that <see cref="FieldSchemaSynchronizer.Synchronize{TEntity, TDto}"/> updates the collection in-place
    /// by removing stale entities, appending new entities, and updating matched entities.
    /// </summary>
    [Fact]
    public void SynchronizeShouldUpdateCollectionInPlace()
    {
        // Arrange
        var keepId = Guid.NewGuid();
        var staleId = Guid.NewGuid();

        var keptEntity = new CustomField { Id = keepId, Key = "keep_me", Name = "Original Keep" };
        var staleEntity = new CustomField { Id = staleId, Key = "remove_me", Name = "Stale" };
        var entityCollection = new List<CustomField> { keptEntity, staleEntity };

        var keepDto = new UpdateCustomFieldDto(keepId, "Updated Keep", "keep_me", FieldDataType.Text, false, 0);
        var newDto = new UpdateCustomFieldDto(null, "Brand New", "brand_new", FieldDataType.Number, true, 1);

        // Act
        FieldSchemaSynchronizer.Synchronize(
            entityCollection,
            [keepDto, newDto],
            id => new EntityNotFoundException<CustomField>(id),
            createEntity: d => new CustomField { Key = d.Key, Name = d.Name, DataType = d.DataType },
            updateEntity: (e, d) => e.Name = d.Name);

        // Assert
        entityCollection.ShouldNotContain(staleEntity);
        entityCollection.Count.ShouldBe(2);

        var matched = entityCollection.Single(f => f.Id == keepId);
        matched.Name.ShouldBe("Updated Keep");

        var added = entityCollection.Single(f => f.Key == "brand_new");
        added.Name.ShouldBe("Brand New");
        added.DataType.ShouldBe(FieldDataType.Number);
    }
}
