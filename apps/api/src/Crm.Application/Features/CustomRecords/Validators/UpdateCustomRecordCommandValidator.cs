using Crm.Application.Dtos.CustomRecords.Commands;
using Crm.Domain.Consts.Entities;
using FluentValidation;

namespace Crm.Application.Features.CustomRecords.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UpdateCustomRecordCommand"/>.
/// </summary>
public class UpdateCustomRecordCommandValidator : AbstractValidator<UpdateCustomRecordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateCustomRecordCommandValidator"/> class.
    /// </summary>
    public UpdateCustomRecordCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Custom record ID is required.");

        RuleFor(x => x.Data)
            .NotNull().WithMessage("Record data payload is required.")
            .Must(data => data == null || data.Count <= CustomRecordValidationConstants.MaxFields)
            .WithMessage($"Record data payload cannot contain more than {CustomRecordValidationConstants.MaxFields} fields.");
    }
}
