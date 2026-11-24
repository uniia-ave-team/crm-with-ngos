namespace Crm.Cli.Abstractions;

/// <summary>
/// Abstracts standard console streams for CLI commands to enable deterministic unit testing.
/// </summary>
public interface ICliConsole
{
    /// <summary>
    /// Gets the standard input reader.
    /// </summary>
    TextReader In { get; }

    /// <summary>
    /// Gets the standard output writer.
    /// </summary>
    TextWriter Out { get; }

    /// <summary>
    /// Gets the standard error writer.
    /// </summary>
    TextWriter Error { get; }

    /// <summary>
    /// Gets a value indicating whether standard input is redirected from a pipe or file.
    /// </summary>
    bool IsInputRedirected { get; }
}
