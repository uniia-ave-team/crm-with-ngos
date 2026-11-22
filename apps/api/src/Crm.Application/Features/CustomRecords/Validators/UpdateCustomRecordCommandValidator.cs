using Crm.Application.Features.CustomRecords.Commands;
using FluentValidation;

namespace Crm.Application.Features.CustomRecords.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UpdateCustomRecordCommand"/>.
/// </summary>
public sealed class UpdateCustomRecordCommandValidator : AbstractValidator<UpdateCustomRecordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateCustomRecordCommandValidator"/> class.
    /// </summary>
    public UpdateCustomRecordCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Custom record ID is required.");

        RuleFor(x => x.Data)
            .ApplyCustomRecordDataRules();
    }
}
