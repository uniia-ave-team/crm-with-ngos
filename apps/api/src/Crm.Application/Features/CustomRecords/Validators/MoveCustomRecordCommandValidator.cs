using Crm.Application.Features.CustomRecords.Commands;
using FluentValidation;

namespace Crm.Application.Features.CustomRecords.Validators;

/// <summary>
/// Provides validation rules for <see cref="MoveCustomRecordCommand"/>.
/// </summary>
public sealed class MoveCustomRecordCommandValidator : AbstractValidator<MoveCustomRecordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MoveCustomRecordCommandValidator"/> class.
    /// </summary>
    public MoveCustomRecordCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Custom record ID is required.");

        this.ApplyRecordPlacementRules(
            x => x.PreviousRecordId,
            x => x.NextRecordId,
            selfIdAccessor: cmd => cmd.Id);
    }
}
