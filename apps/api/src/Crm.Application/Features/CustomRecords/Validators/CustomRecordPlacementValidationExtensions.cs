using System.Linq.Expressions;
using FluentValidation;

namespace Crm.Application.Features.CustomRecords.Validators;

/// <summary>
/// Provides reusable FluentValidation rules for optional manual row placement neighbor identifiers
/// (<c>PreviousRecordId</c> and <c>NextRecordId</c>) shared across custom record create and move commands.
/// </summary>
internal static class CustomRecordPlacementValidationExtensions
{
    /// <summary>
    /// Applies shared non-empty, distinct, and optional non-self-referencing validation rules to
    /// optional preceding and following record neighbor identifiers.
    /// </summary>
    /// <typeparam name="TCommand">The custom record command type.</typeparam>
    /// <param name="validator">The target FluentValidation validator.</param>
    /// <param name="previousSelector">Expression selecting the optional preceding record identifier.</param>
    /// <param name="nextSelector">Expression selecting the optional following record identifier.</param>
    /// <param name="selfIdAccessor">Optional accessor returning the moving record's own identifier to forbid self-references.</param>
    public static void ApplyRecordPlacementRules<TCommand>(
        this AbstractValidator<TCommand> validator,
        Expression<Func<TCommand, Guid?>> previousSelector,
        Expression<Func<TCommand, Guid?>> nextSelector,
        Func<TCommand, Guid>? selfIdAccessor = null)
    {
        var getPrevious = previousSelector.Compile();
        var getNext = nextSelector.Compile();

        var previousRule = validator.RuleFor(previousSelector)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("Previous record ID cannot be an empty GUID.");

        var nextRule = validator.RuleFor(nextSelector)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("Next record ID cannot be an empty GUID.");

        if (selfIdAccessor is not null)
        {
            previousRule
                .Must((cmd, prevId) => !prevId.HasValue || prevId.Value != selfIdAccessor(cmd))
                .WithMessage("Previous record ID cannot reference the record itself.");

            nextRule
                .Must((cmd, nextId) => !nextId.HasValue || nextId.Value != selfIdAccessor(cmd))
                .WithMessage("Next record ID cannot reference the record itself.");
        }

        validator.RuleFor(x => x)
            .Must(cmd =>
            {
                Guid? prev = getPrevious(cmd);
                Guid? next = getNext(cmd);
                return !prev.HasValue || !next.HasValue || prev != next;
            })
            .WithMessage("Previous and next record IDs cannot be the same.");
    }
}
