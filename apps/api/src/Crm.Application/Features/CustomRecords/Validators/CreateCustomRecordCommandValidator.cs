using Crm.Application.Features.CustomRecords.Commands;
using FluentValidation;

namespace Crm.Application.Features.CustomRecords.Validators;

/// <summary>
/// Provides validation rules for the <see cref="CreateCustomRecordCommand"/>.
/// </summary>
public sealed class CreateCustomRecordCommandValidator : AbstractValidator<CreateCustomRecordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCustomRecordCommandValidator"/> class.
    /// </summary>
    public CreateCustomRecordCommandValidator()
    {
        RuleFor(x => x.CustomTableId)
            .NotEmpty().WithMessage("Custom table ID is required.");

        RuleFor(x => x.Data)
            .ApplyCustomRecordDataRules();

        this.ApplyRecordPlacementRules(
            x => x.PreviousRecordId,
            x => x.NextRecordId);
    }
}
