using Crm.Application.Dtos.CustomTables.Commands;
using FluentValidation;

namespace Crm.Application.Features.CustomTables.Validators;

/// <summary>
/// Provides validation rules for the <see cref="DeleteCustomTableCommand"/>.
/// </summary>
public class DeleteCustomTableCommandValidator : AbstractValidator<DeleteCustomTableCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteCustomTableCommandValidator"/> class.
    /// </summary>
    public DeleteCustomTableCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Table ID is required.");
    }
}
