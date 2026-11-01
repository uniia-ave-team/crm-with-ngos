using Crm.Application.Common.Validators;
using Crm.Application.Dtos.CustomTables;
using Crm.Domain.Enums;
using FluentValidation.TestHelper;

namespace Crm.Application.Tests.Features.CustomTables.Validators;

public class FieldValidationRulesTests
{
    private readonly FieldValidationRules<CreateCustomFieldDto> _validator = new();

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
}
