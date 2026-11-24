using Crm.Application.Common.Consts;
using Crm.Application.Features.Projects.Commands;
using Crm.Domain.Extensions;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="CreateProjectCommand"/>.
/// </summary>
public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateProjectCommandValidator"/> class.
    /// </summary>
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.Title)
            .ValidProjectTitle();

        RuleFor(x => x.Description)
            .ValidProjectDescription();

        RuleFor(x => x.Visibility)
            .IsInEnum().WithMessage(ValidationMessages.InvalidProjectVisibility);

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage(ValidationMessages.StartDateRequired);

        RuleFor(x => x.EndDate)
            .Must((command, endDate) => endDate!.Value.ToUtc() >= command.StartDate.ToUtc())
            .WithMessage(ValidationMessages.EndDateBeforeStartDate)
            .When(x => x.EndDate.HasValue);
    }
}
