using Crm.Cli.Abstractions;

namespace Crm.Cli.Services;

/// <summary>
/// Default <see cref="ICliConsole"/> implementation backed by <see cref="Console"/>.
/// </summary>
public sealed class SystemCliConsole : ICliConsole
{
    /// <inheritdoc />
    public TextReader In => Console.In;

    /// <inheritdoc />
    public TextWriter Out => Console.Out;

    /// <inheritdoc />
    public TextWriter Error => Console.Error;

    /// <inheritdoc />
    public bool IsInputRedirected => Console.IsInputRedirected;
}
