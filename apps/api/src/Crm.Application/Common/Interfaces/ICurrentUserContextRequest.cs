namespace Crm.Application.Common.Interfaces;

/// <summary>
/// Base marker interface for Mediator requests that require authenticated user context
/// populated by <see cref="Behaviors.CurrentUserContextBehavior{TRequest, TResponse}"/>.
/// </summary>
public interface ICurrentUserContextRequest;
