using Crm.Cli.Models;

namespace Crm.Cli.Abstractions;

/// <summary>
/// Defines the contract for an executable command-line operation within the CLI application.
/// </summary>
public interface ICliCommand
{
    /// <summary>
    /// Gets the unique command identifier used to invoke this operation from the command line.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a brief summary explaining the command's purpose, displayed in the CLI help menu.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes the business logic associated with this command using the supplied parameters.
    /// </summary>
    /// <param name="args">The parsed arguments and parameters supplied to the application.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An exit code representing the execution status (0 indicates success; any non-zero value indicates failure).</returns>
    Task<int> ExecuteAsync(CliArguments args, CancellationToken cancellationToken = default);
}
