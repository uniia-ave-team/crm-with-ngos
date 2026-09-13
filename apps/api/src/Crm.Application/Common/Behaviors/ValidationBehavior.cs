using FluentValidation;
using MediatR;

namespace Crm.Application.Common.Behaviors;

/// <summary>
/// Represents a MediatR pipeline behavior that intercepts incoming requests
/// and automatically validates them using all registered <see cref="IValidator{T}"/> instances
/// for the given request type.
/// </summary>
/// <typeparam name="TRequest">The type of the request being handled.</typeparam>
/// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ValidationBehavior{TRequest, TResponse}"/> class.
/// </remarks>
/// <param name="validators">The collection of validators registered for the request type.</param>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Handles the incoming request by executing all associated validators asynchronously in parallel.
    /// If any validation rules fail, a <see cref="ValidationException"/> containing all error details is thrown.
    /// Otherwise, the execution proceeds to the next handler in the pipeline.
    /// </summary>
    /// <param name="request">The incoming request object being processed.</param>
    /// <param name="next">The delegate representing the next behavior or handler in the MediatR pipeline.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The response from the next handler if validation succeeds.</returns>
    /// <exception cref="ValidationException">Thrown when one or more validation errors occur during execution.</exception>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        return failures.Count != 0 ? throw new ValidationException(failures) : await next(cancellationToken);
    }
}
