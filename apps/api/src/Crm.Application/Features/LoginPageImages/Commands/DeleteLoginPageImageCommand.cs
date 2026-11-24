using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.LoginPageImages.Commands;

/// <summary>
/// Represents a command to delete an existing login page image.
/// </summary>
/// <param name="Id">The unique identifier of the login page image to delete.</param>
public record DeleteLoginPageImageCommand(Guid Id) : ICommand, ITransactionalCommand;
