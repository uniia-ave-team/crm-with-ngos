using Crm.Application.Common.Validators;
using Crm.Application.Dtos.CustomTables;
using Crm.Domain.Enums;
using FluentValidation.TestHelper;
using Shouldly;

namespace Crm.Application.Tests.Features.CustomTables.Validators;

/// <summary>
/// Contains unit tests for <see cref="FieldValidationRules{T}"/> and <see cref="FieldListValidator{T}"/>.
/// </summary>
public class FieldValidationRulesTests
{
    private readonly FieldValidationRules<CreateCustomFieldDto> _validator = new();
    private readonly FieldListValidator<CreateCustomFieldDto> _listValidator = new();

    /// <summary>
    /// Verifies that valid snake_case field keys pass validation.
    /// </summary>
    /// <param name="key">The valid snake_case key to test.</param>
    [Theory]
    [InlineData("valid_key")]
    [InlineData("status")]
    [InlineData("first_name")]
    [InlineData("field1")]
    public void KeyWhenValidSnakeCaseShouldNotHaveValidationError(string key)
    {
        // Arrange
        var model = new CreateCustomFieldDto("Field", key, FieldDataType.Text, false, 0);

        // Act
        var result = _validator.TestValidate(model);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Key);
    }

    /// <summary>
    /// Verifies that reserved entity property names are rejected as field keys.
    /// </summary>
    /// <param name="reservedKey">The reserved property name to test.</param>
    [Theory]
    [InlineData("Id")]
    [InlineData("id")]
    [InlineData("CustomTableId")]
    [InlineData("customtableid")]
    [InlineData("CustomTable")]
    [InlineData("customtable")]
    [InlineData("Data")]
    [InlineData("data")]
    public void KeyWhenReservedEntityPropertyShouldHaveValidationError(string reservedKey)
    {
        // Arrange
        var model = new CreateCustomFieldDto("Field", reservedKey, FieldDataType.Text, false, 0);

        // Act
        var result = _validator.TestValidate(model);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Key);
    }

    /// <summary>
    /// Verifies that a field list containing a null element fails validation.
    /// </summary>
    [Fact]
    public void FieldsWhenContainingNullElementShouldHaveValidationError()
    {
        // Arrange
        List<CreateCustomFieldDto> fields =
        [
            new CreateCustomFieldDto("Field One", "field_one", FieldDataType.Text, false, 0),
            null!,
        ];

        // Act
        var result = _listValidator.Validate(fields);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies that non-sequential field display orders fail validation.
    /// </summary>
    [Fact]
    public void FieldsWhenOrdersAreNotSequentialShouldHaveValidationError()
    {
        // Arrange
        List<CreateCustomFieldDto> fields =
        [
            new CreateCustomFieldDto("Field One", "field_one", FieldDataType.Text, false, 0),
            new CreateCustomFieldDto("Field Two", "field_two", FieldDataType.Number, false, 2),
        ];

        // Act
        var result = _listValidator.Validate(fields);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// Verifies that sequential zero-based field display orders pass validation.
    /// </summary>
    [Fact]
    public void FieldsWhenOrdersAreSequentialFromZeroShouldNotHaveValidationError()
    {
        // Arrange
        List<CreateCustomFieldDto> fields =
        [
            new CreateCustomFieldDto("Field One", "field_one", FieldDataType.Text, false, 0),
            new CreateCustomFieldDto("Field Two", "field_two", FieldDataType.Number, true, 1),
        ];

        // Act
        var result = _listValidator.Validate(fields);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
