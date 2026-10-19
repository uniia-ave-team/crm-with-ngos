using Crm.Cli.Abstractions;
using Crm.Cli.Constants;
using Crm.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Crm.Cli;

/// <summary>
/// Orchestrates CLI command discovery, parameter validation, and execution dispatching.
/// </summary>
/// <param name="commands">The collection of available CLI commands registered in the service provider.</param>
/// <param name="logger">The logger instance used for routing diagnostics and operational feedback.</param>
public partial class CliRunner(
    IEnumerable<ICliCommand> commands,
    ILogger<CliRunner> logger)
{
    private const string HelpOption = "help";
    private const string HelpAlias = "h";

    /// <summary>
    /// Dispatches the execution request to the appropriate command based on user input.
    /// </summary>
    /// <param name="args">The parsed command-line arguments.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The exit code returned by the command execution or routing validation.</returns>
    public async Task<int> RunAsync(CliArguments args, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(args.CommandName) || args.HasFlag(HelpOption, HelpAlias))
        {
            PrintHelp();
            return 0;
        }

        var command = commands.FirstOrDefault(c => c.Name.Equals(args.CommandName, StringComparison.OrdinalIgnoreCase));
        if (command is null)
        {
            LogCommandNotFound(logger, args.CommandName);
            PrintHelp();
            return 1;
        }

        return await command.ExecuteAsync(args, cancellationToken);
    }

    /// <summary>
    /// Outputs the list of registered CLI commands and general usage instructions to the console.
    /// </summary>
    private void PrintHelp()
    {
        Console.WriteLine("Available commands:");

        foreach (var cmd in commands)
        {
            Console.WriteLine($"  {cmd.Name,-25} {cmd.Description}");
        }
    }

    [LoggerMessage(EventId = CliLogEventIds.CommandNotFound, Level = LogLevel.Error, Message = "Unknown command: '{CommandName}'.")]
    private static partial void LogCommandNotFound(ILogger logger, string commandName);
}
