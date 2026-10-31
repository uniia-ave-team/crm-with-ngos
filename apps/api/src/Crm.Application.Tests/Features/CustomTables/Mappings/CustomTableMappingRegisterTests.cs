using Crm.Application.Dtos.CustomTables;
using Crm.Application.Dtos.CustomTables.Commands;
using Crm.Application.Features.CustomTables.Mappings;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Mapster;
using Shouldly;

namespace Crm.Application.Tests.Features.CustomTables.Mappings;

/// <summary>
/// Contains unit tests verifying Mapster mapping registrations for custom table entities and DTOs.
/// </summary>
public class CustomTableMappingRegisterTests
{
    private readonly TypeAdapterConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomTableMappingRegisterTests"/> class.
    /// Sets up an isolated <see cref="TypeAdapterConfig"/> instance with <see cref="CustomTableMappingRegister"/>.
    /// </summary>
    public CustomTableMappingRegisterTests()
    {
        _config = new TypeAdapterConfig();
        new CustomTableMappingRegister().Register(_config);
    }

    /// <summary>
    /// Verifies that adapting an <see cref="UpdateCustomFieldDto"/> with a <see langword="null"/> ID
    /// onto an existing <see cref="CustomField"/> preserves the target entity's existing identifier.
    /// </summary>
    [Fact]
    public void AdaptUpdateCustomFieldDtoWhenIdIsNullShouldPreserveTargetId()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var existingField = new CustomField
        {
            Id = existingId,
            CustomTableId = tableId,
            Key = "status",
            Name = "Initial Status",
            DataType = FieldDataType.Text,
            IsRequired = false,
            Order = 1,
        };

        var dto = new UpdateCustomFieldDto(
            null,
            "Updated Status",
            "status",
            FieldDataType.Text,
            true,
            2);

        // Act
        dto.Adapt(existingField, _config);

        // Assert
        existingField.Id.ShouldBe(existingId);
        existingField.CustomTableId.ShouldBe(tableId);
        existingField.Name.ShouldBe("Updated Status");
        existingField.IsRequired.ShouldBeTrue();
        existingField.Order.ShouldBe((byte)2);
    }

    /// <summary>
    /// Verifies that adapting an <see cref="UpdateCustomFieldDto"/> onto an existing <see cref="CustomField"/>
    /// ignores the foreign key and navigation properties.
    /// </summary>
    [Fact]
    public void AdaptUpdateCustomFieldDtoShouldIgnoreCustomTableNavigationAndForeignKey()
    {
        // Arrange
        var originalTableId = Guid.NewGuid();
        var originalTable = new CustomTable { Id = originalTableId, Name = "Original Table" };
        var existingField = new CustomField
        {
            Id = Guid.NewGuid(),
            CustomTableId = originalTableId,
            CustomTable = originalTable,
            Key = "notes",
            Name = "Notes",
        };

        var dto = new UpdateCustomFieldDto(
            Guid.NewGuid(),
            "Modified Notes",
            "notes",
            FieldDataType.Text,
            false,
            0);

        // Act
        dto.Adapt(existingField, _config);

        // Assert
        existingField.CustomTableId.ShouldBe(originalTableId);
        existingField.CustomTable.ShouldBeSameAs(originalTable);
        existingField.Name.ShouldBe("Modified Notes");
    }

    /// <summary>
    /// Verifies that adapting an <see cref="UpdateCustomTableCommand"/> onto an existing <see cref="CustomTable"/>
    /// updates table metadata while ignoring the entity identifier and fields collection.
    /// </summary>
    [Fact]
    public void AdaptUpdateCustomTableCommandShouldUpdateMetadataAndIgnoreIdAndFields()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var existingFields = new List<CustomField>
        {
            new() { Id = Guid.NewGuid(), Key = "field_one", Name = "Field One" },
        };
        var existingTable = new CustomTable
        {
            Id = existingId,
            Name = "Old Name",
            Description = "Old Description",
            Fields = existingFields,
        };

        var command = new UpdateCustomTableCommand(
            Guid.NewGuid(),
            "New Name",
            "New Description",
            [
                new UpdateCustomFieldDto(Guid.NewGuid(), "New Field", "field_new", FieldDataType.Number, false, 0),
            ]);

        // Act
        command.Adapt(existingTable, _config);

        // Assert
        existingTable.Id.ShouldBe(existingId);
        existingTable.Name.ShouldBe("New Name");
        existingTable.Description.ShouldBe("New Description");
        existingTable.Fields.ShouldBeSameAs(existingFields);
        existingTable.Fields.Count.ShouldBe(1);
    }
}
