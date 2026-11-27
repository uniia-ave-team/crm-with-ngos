using Crm.Application.Common.Validators;
using Crm.Application.Dtos.Common;
using Crm.Application.Dtos.CustomTables;
using Crm.Domain.Constants.Validation;
using FluentValidation.TestHelper;
using Shouldly;

namespace Crm.Application.Tests.Features.CustomTables.Validators;

/// <summary>
/// Contains unit tests for <see cref="FieldValidationRules{T}"/>, <see cref="FieldListValidator{T}"/>,
/// and <see cref="IdentifiableFieldListValidator{T}"/>.
/// </summary>
public class FieldValidationRulesTests
{
    private readonly FieldValidationRules<FieldDefinitionDto> _validator = new();
    private readonly FieldListValidator<FieldDefinitionDto> _listValidator = new();
    private readonly IdentifiableFieldListValidator<UpdateCustomFieldDto> _identifiableListValidator = new();

    [Fact]
    public void NameWhenValidShouldNotHaveValidationError()
    {
        var model = new FieldDefinitionDto("Назва колонки", false, 0);

        var result = _validator.TestValidate(model);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\0name")]
    public void NameWhenBlankOrContainsNullCharShouldHaveValidationError(string invalidName)
    {
        var model = new FieldDefinitionDto(invalidName, false, 0);

        var result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void NameWhenExceedingMaxLengthShouldHaveValidationError()
    {
        var model = new FieldDefinitionDto(new string('A', FieldSchemaValidationConstants.MaxNameLength + 1), false, 0);

        var result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void FieldsWhenContainingNullElementShouldHaveValidationError()
    {
        List<FieldDefinitionDto> fields =
        [
            new FieldDefinitionDto("Field One", false, 0),
            null!,
        ];

        var result = _listValidator.Validate(fields);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void FieldsWhenOrdersAreNotSequentialShouldHaveValidationError()
    {
        List<FieldDefinitionDto> fields =
        [
            new FieldDefinitionDto("Field One", false, 0),
            new FieldDefinitionDto("Field Two", false, 2),
        ];

        var result = _listValidator.Validate(fields);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void FieldsWhenOrdersAreSequentialFromZeroShouldNotHaveValidationError()
    {
        List<FieldDefinitionDto> fields =
        [
            new FieldDefinitionDto("Field One", false, 0),
            new FieldDefinitionDto("Field Two", true, 1),
        ];

        var result = _listValidator.Validate(fields);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void IdentifiableFieldsShouldRejectEmptyAndDuplicateIdsAndAcceptNullOrDistinctIds()
    {
        var id = Guid.CreateVersion7();

        _identifiableListValidator.Validate([new UpdateCustomFieldDto(Guid.Empty, "Bad", false, 0)])
            .IsValid.ShouldBeFalse();

        _identifiableListValidator.Validate(
            [
                new UpdateCustomFieldDto(id, "One", false, 0),
                new UpdateCustomFieldDto(id, "Two", false, 1),
            ])
            .IsValid.ShouldBeFalse();

        _identifiableListValidator.Validate(
            [
                new UpdateCustomFieldDto(id, "Existing", false, 0),
                new UpdateCustomFieldDto(null, "New", true, 1),
            ])
            .IsValid.ShouldBeTrue();
    }
}
