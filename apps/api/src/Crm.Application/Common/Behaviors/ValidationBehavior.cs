using FluentValidation;
using FluentValidation.Results;
using Mediator;

namespace Crm.Application.Common.Behaviors;

/// <summary>
/// Represents a Mediator pipeline behavior that intercepts incoming requests
/// and automatically validates them using all registered <see cref="IValidator{T}"/> instances
/// for the given request type.
/// </summary>
/// <typeparam name="TRequest">The type of the request being handled.</typeparam>
/// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
/// <param name="validators">The collection of validators registered for the request type.</param>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    /// <summary>
    /// Handles the incoming request by executing all associated validators asynchronously.
    /// If any validation rules fail, a <see cref="ValidationException"/> containing all error details is thrown.
    /// Otherwise, the execution proceeds to the next handler in the pipeline.
    /// </summary>
    /// <param name="message">The incoming request object being processed.</param>
    /// <param name="next">The delegate representing the next behavior or handler in the Mediator pipeline.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The response from the next handler if validation succeeds.</returns>
    /// <exception cref="ValidationException">Thrown when one or more validation errors occur during execution.</exception>
    public async ValueTask<TResponse> Handle(
        TRequest message,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (validators is ICollection<IValidator<TRequest>> { Count: 0 })
        {
            return await next(message, cancellationToken);
        }

        IReadOnlyList<IValidator<TRequest>> validatorList = validators as IReadOnlyList<IValidator<TRequest>> ?? [.. validators];
        if (validatorList.Count == 0)
        {
            return await next(message, cancellationToken);
        }

        var context = new ValidationContext<TRequest>(message);

        if (validatorList.Count == 1)
        {
            ValidationResult singleResult = await validatorList[0].ValidateAsync(context, cancellationToken);
            return !singleResult.IsValid
                ? throw new ValidationException(singleResult.Errors)
                : await next(message, cancellationToken);
        }

        ValidationResult[] validationResults = await Task.WhenAll(
            validatorList.Select(v => v.ValidateAsync(context, cancellationToken)));

        List<ValidationFailure> failures = [
            .. validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f is not null),
        ];

        return failures.Count != 0
            ? throw new ValidationException(failures)
            : await next(message, cancellationToken);
    }
}
