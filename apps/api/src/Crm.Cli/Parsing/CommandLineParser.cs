using Crm.Cli.Models;

namespace Crm.Cli.Parsing;

/// <summary>
/// Utility class responsible for tokenizing, normalizing, and mapping raw command-line input strings
/// into a structured <see cref="CliArguments"/> model.
/// </summary>
/// <remarks>
/// Supports the following CLI conventions:
/// <list type="bullet">
///   <item><description>Root commands without prefixes: <c>crm users</c></description></item>
///   <item><description>Inline key-value options: <c>--timeout=30</c> or <c>-t=30</c></description></item>
///   <item><description>Separated key-value options: <c>--timeout 30</c> or <c>-t 30</c></description></item>
///   <item><description>Standalone boolean flags: <c>--force</c> or <c>-f</c> (evaluates to <c>"true"</c>)</description></item>
///   <item><description>Positional operands: values not preceded by an option key (e.g., entity IDs, file paths)</description></item>
///   <item><description>End-of-options delimiter (<c>--</c>): forces all subsequent tokens to be treated strictly as operands</description></item>
///   <item><description>Negative numeric arguments: values like <c>-5</c> or <c>-10.5</c> are treated as values, not flags</description></item>
/// </list>
/// </remarks>
public static class CommandLineParser
{
    private const string InlineDelimiter = "=";
    private const string EndOfOptionsDelimiter = "--";
    private const string TrueFlagValue = "true";

    /// <summary>
    /// Parses an array of raw command-line argument strings into a strongly typed <see cref="CliArguments"/> instance.
    /// </summary>
    /// <param name="args">The raw string array passed to the application entry point (e.g., from <c>Main</c>).</param>
    /// <param name="booleanFlags">
    /// An optional set of known boolean flag identifiers (e.g., <c>"force"</c>, <c>"v"</c>).
    /// When specified, these switches will never consume subsequent arguments as their value, preventing operand hijacking.
    /// </param>
    /// <returns>
    /// A populated <see cref="CliArguments"/> object containing the extracted command name,
    /// parsed parameters, and positional operands. Returns an empty model if <paramref name="args"/> is null or empty.
    /// </returns>
    public static CliArguments Parse(string[]? args, IReadOnlySet<string>? booleanFlags = null)
    {
        if (args is null || args.Length == 0)
        {
            return new CliArguments(null);
        }

        var (commandName, startIndex) = ExtractCommandName(args);
        var (parameters, operands) = TokenizeArguments(args, startIndex, booleanFlags);

        return new CliArguments(commandName, parameters, operands);
    }

    private static (string? CommandName, int StartIndex) ExtractCommandName(string[] args) =>
        !args[0].StartsWith('-')
            ? (args[0], 1)
            : (null, 0);

    private static (Dictionary<string, string> Parameters, List<string> Operands) TokenizeArguments(
        string[] args,
        int startIndex,
        IReadOnlySet<string>? booleanFlags)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var operands = new List<string>();
        var treatRemainingAsOperands = false;

        for (var i = startIndex; i < args.Length; i++)
        {
            var token = args[i];

            if (treatRemainingAsOperands)
            {
                operands.Add(token);
                continue;
            }

            if (token == EndOfOptionsDelimiter)
            {
                treatRemainingAsOperands = true;
                continue;
            }

            if (!IsOption(token))
            {
                operands.Add(token);
                continue;
            }

            ConsumeOption(args, ref i, parameters, booleanFlags);
        }

        return (parameters, operands);
    }

    private static void ConsumeOption(
        string[] args,
        ref int index,
        Dictionary<string, string> parameters,
        IReadOnlySet<string>? booleanFlags)
    {
        var token = args[index];

        if (token.Contains(InlineDelimiter))
        {
            var (key, value) = ParseInlineOption(token);

            if (!string.IsNullOrWhiteSpace(key))
            {
                parameters[key] = value;
            }

            return;
        }

        var optionKey = NormalizeKey(token);
        if (string.IsNullOrWhiteSpace(optionKey))
        {
            return;
        }

        var canConsumeValue = !IsKnownBooleanFlag(optionKey, booleanFlags)
                              && index + 1 < args.Length
                              && CanBeConsumedAsValue(args[index + 1]);

        parameters[optionKey] = canConsumeValue ? args[++index] : TrueFlagValue;
    }

    private static (string Key, string Value) ParseInlineOption(string token)
    {
        var parts = token.Split(InlineDelimiter, 2);
        return (NormalizeKey(parts[0]), parts[1]);
    }

    private static bool IsKnownBooleanFlag(string key, IReadOnlySet<string>? booleanFlags) =>
        booleanFlags is not null
        && (booleanFlags.Contains(key) || booleanFlags.Any(f => string.Equals(f, key, StringComparison.OrdinalIgnoreCase)));

    private static bool CanBeConsumedAsValue(string token) =>
        token != EndOfOptionsDelimiter && !IsOption(token);

    private static bool IsOption(string token) =>
        token.Length > 1
        && token.StartsWith('-')
        && token != EndOfOptionsDelimiter
        && !char.IsDigit(token[1]);

    private static string NormalizeKey(string key) => key.TrimStart('-');
}
