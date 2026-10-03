using Crm.Application.Interfaces;
using MediatR;

namespace Crm.Application.Dtos.LoginPageImage.Commands;

/// <summary>
/// Represents a command to create a new login page image in the system.
/// </summary>
/// <param name="Url">The URL or storage path of the image to be created.</param>
/// <returns>The unique identifier of the newly created login page image.</returns>
public record CreateLoginPageImageCommand(
    string Url) : IRequest<Guid>, ITransactionalCommand;
