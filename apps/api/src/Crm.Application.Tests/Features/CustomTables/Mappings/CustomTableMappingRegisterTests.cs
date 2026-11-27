using Crm.Application.Dtos.CustomTables;
using Crm.Application.Features.CustomTables.Commands;
using Crm.Application.Features.CustomTables.Mappings;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Entities;
using Mapster;
using Shouldly;

namespace Crm.Application.Tests.Features.CustomTables.Mappings;

/// <summary>
/// Contains unit tests verifying Mapster mapping registrations for custom table entities and DTOs.
/// </summary>
public class CustomTableMappingRegisterTests
{
    private readonly TypeAdapterConfig _config;

    public CustomTableMappingRegisterTests()
    {
        _config = new TypeAdapterConfig();
        new CustomTableMappingRegister().Register(_config);
    }

    [Fact]
    public void AdaptUpdateCustomFieldDtoWhenIdIsNullShouldPreserveTargetId()
    {
        // Arrange
        var existingId = Guid.CreateVersion7();
        var tableId = Guid.CreateVersion7();
        var existingField = new CustomField
        {
            Id = existingId,
            CustomTableId = tableId,
            Name = "Initial Status",
            IsRequired = false,
            Order = 1,
        };

        var dto = new UpdateCustomFieldDto(null, "Updated Status", true, 2);

        // Act
        dto.Adapt(existingField, _config);

        // Assert
        existingField.Id.ShouldBe(existingId);
        existingField.CustomTableId.ShouldBe(tableId);
        existingField.Name.ShouldBe("Updated Status");
        existingField.IsRequired.ShouldBeTrue();
        existingField.Order.ShouldBe((byte)2);
    }

    [Fact]
    public void AdaptUpdateCustomFieldDtoShouldIgnoreCustomTableNavigationAndForeignKey()
    {
        // Arrange
        var originalTableId = Guid.CreateVersion7();
        var originalTable = new CustomTable { Id = originalTableId, Name = "Original Table" };
        var existingField = new CustomField
        {
            Id = Guid.CreateVersion7(),
            CustomTableId = originalTableId,
            CustomTable = originalTable,
            Name = "Notes",
        };

        var dto = new UpdateCustomFieldDto(Guid.CreateVersion7(), "Modified Notes", false, 0);

        // Act
        dto.Adapt(existingField, _config);

        // Assert
        existingField.CustomTableId.ShouldBe(originalTableId);
        existingField.CustomTable.ShouldBeSameAs(originalTable);
        existingField.Name.ShouldBe("Modified Notes");
    }

    [Fact]
    public void AdaptUpdateCustomTableCommandShouldUpdateMetadataAndIgnoreIdAndFields()
    {
        // Arrange
        var existingId = Guid.CreateVersion7();
        var existingFields = new List<CustomField>
        {
            new() { Id = Guid.CreateVersion7(), Name = "Field One" },
        };
        var existingTable = new CustomTable
        {
            Id = existingId,
            Name = "Old Name",
            Description = "Old Description",
            Fields = existingFields,
        };

        var command = new UpdateCustomTableCommand(
            "New Name",
            "New Description",
            [
                new UpdateCustomFieldDto(Guid.CreateVersion7(), "New Field", false, 0),
            ])
        {
            Id = Guid.CreateVersion7(),
        };

        // Act
        command.Adapt(existingTable, _config);

        // Assert
        existingTable.Id.ShouldBe(existingId);
        existingTable.Name.ShouldBe("New Name");
        existingTable.Description.ShouldBe("New Description");
        existingTable.Fields.ShouldBeSameAs(existingFields);
        existingTable.Fields.Count.ShouldBe(1);
    }

    [Fact]
    public void AdaptIdentifiableFieldInterfaceWhenIdIsNullShouldPreserveTargetId()
    {
        // Arrange
        var existingId = Guid.CreateVersion7();
        var tableId = Guid.CreateVersion7();
        var existingField = new CustomField
        {
            Id = existingId,
            CustomTableId = tableId,
            Name = "City",
            IsRequired = false,
            Order = 0,
        };

        IIdentifiableField dto = new UpdateCustomFieldDto(null, "Updated City", true, 1);

        // Act
        dto.Adapt(existingField, _config);

        // Assert
        existingField.Id.ShouldBe(existingId);
        existingField.CustomTableId.ShouldBe(tableId);
        existingField.Name.ShouldBe("Updated City");
        existingField.IsRequired.ShouldBeTrue();
        existingField.Order.ShouldBe((byte)1);
    }
}
