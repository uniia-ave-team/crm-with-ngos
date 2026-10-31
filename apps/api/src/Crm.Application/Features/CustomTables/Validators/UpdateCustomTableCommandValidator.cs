using Crm.Application.Common.Validators;
using Crm.Application.Dtos.CustomTables;
using Crm.Application.Dtos.CustomTables.Commands;
using Crm.Domain.Consts.Entities.ValidationConstants;
using FluentValidation;

namespace Crm.Application.Features.CustomTables.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UpdateCustomTableCommand"/>.
/// </summary>
public class UpdateCustomTableCommandValidator : AbstractValidator<UpdateCustomTableCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateCustomTableCommandValidator"/> class.
    /// </summary>
    public UpdateCustomTableCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Table ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Table name is required.")
            .MaximumLength(CustomTableValidationConstants.MaxNameLength)
            .WithMessage($"Table name must not exceed {CustomTableValidationConstants.MaxNameLength} characters.");

        RuleFor(x => x.Description)
            .MaximumLength(CustomTableValidationConstants.MaxDescriptionLength)
            .WithMessage($"Description must not exceed {CustomTableValidationConstants.MaxDescriptionLength} characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Fields)
            .NotEmpty().WithMessage("At least one field is required.")
            .SetValidator(new IdentifiableFieldListValidator<UpdateCustomFieldDto>());
    }
}
