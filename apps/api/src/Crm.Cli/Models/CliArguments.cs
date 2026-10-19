namespace Crm.Cli.Models;

/// <summary>
/// Encapsulates parsed command-line arguments, separating the target command name from its options, flags, and positional operands.
/// </summary>
public record CliArguments(
    string? CommandName,
    IReadOnlyDictionary<string, string>? Parameters = null,
    IReadOnlyList<string>? Operands = null)
{
    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>(Parameters ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Operands { get; init; } = Operands ?? [];

    /// <summary>
    /// Retrieves the parameter value associated with the first matching alias from the options dictionary.
    /// </summary>
    public string? GetValue(params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            var normalized = alias.TrimStart('-');
            if (Parameters.TryGetValue(normalized, out var value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether a boolean flag is present in the arguments and evaluates to true.
    /// </summary>
    public bool HasFlag(params string[] aliases)
    {
        var value = GetValue(aliases);
        return value is not null && !IsFalseValue(value);
    }

    private static bool IsFalseValue(string value) =>
        value.Equals("false", StringComparison.OrdinalIgnoreCase)
        || value.Equals("0", StringComparison.OrdinalIgnoreCase)
        || value.Equals("no", StringComparison.OrdinalIgnoreCase)
        || value.Equals("off", StringComparison.OrdinalIgnoreCase);
}
