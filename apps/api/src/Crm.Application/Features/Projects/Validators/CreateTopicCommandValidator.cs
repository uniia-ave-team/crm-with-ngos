using Crm.Application.Common.Consts;
using Crm.Application.Features.Projects.Commands;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="CreateTopicCommand"/>.
/// </summary>
public sealed class CreateTopicCommandValidator : AbstractValidator<CreateTopicCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTopicCommandValidator"/> class.
    /// </summary>
    public CreateTopicCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.Title)
            .ValidTopicTitle();
    }
}
