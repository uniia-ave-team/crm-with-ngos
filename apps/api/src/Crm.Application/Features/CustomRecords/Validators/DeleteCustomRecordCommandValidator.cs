using Crm.Application.Dtos.CustomRecords.Commands;
using FluentValidation;

namespace Crm.Application.Features.CustomRecords.Validators;

/// <summary>
/// Provides validation rules for the <see cref="DeleteCustomRecordCommand"/>.
/// </summary>
public class DeleteCustomRecordCommandValidator : AbstractValidator<DeleteCustomRecordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteCustomRecordCommandValidator"/> class.
    /// </summary>
    public DeleteCustomRecordCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Custom record ID is required.");
    }
}
