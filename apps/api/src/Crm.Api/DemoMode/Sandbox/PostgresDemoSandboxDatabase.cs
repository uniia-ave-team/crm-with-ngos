using Crm.Infrastructure.Consts;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// PostgreSQL implementation of <see cref="IDemoSandboxDatabase"/> based on
/// <c>CREATE DATABASE … TEMPLATE …</c>. The database user needs the <c>CREATEDB</c> privilege.
/// </summary>
public sealed partial class PostgresDemoSandboxDatabase : IDemoSandboxDatabase
{
    private const string MaintenanceDatabase = "postgres";
    private const string SandboxInfix = "_demo_";
    private const string TemplateSuffix = "template";
    private const int MaxIdentifierLength = 63;
    private const int TemplateAttempts = 5;
    private const int IdleLifetimeSeconds = 20;
    private const int PruningIntervalSeconds = 10;
    private static readonly TimeSpan TemplateRetryDelay = TimeSpan.FromSeconds(1);

    private readonly ILogger<PostgresDemoSandboxDatabase> _logger;
    private readonly NpgsqlConnectionStringBuilder _baseBuilder;
    private readonly string _baseDatabase;
    private readonly string _templateDatabase;
    private readonly int _maxPoolSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresDemoSandboxDatabase"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration holding the connection string.</param>
    /// <param name="options">The demo mode options.</param>
    /// <param name="logger">The logger.</param>
    public PostgresDemoSandboxDatabase(
        IConfiguration configuration,
        IOptions<DemoModeOptions> options,
        ILogger<PostgresDemoSandboxDatabase> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _maxPoolSize = options.Value.Sandbox.MaxPoolSize;

        string connectionString = configuration.GetConnectionString(ConfigurationKeys.PostgreSqlConnection)
            ?? throw new InvalidOperationException("Demo Mode needs the PostgreSQL connection string.");

        _baseBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        _baseDatabase = _baseBuilder.Database
            ?? throw new InvalidOperationException("Demo Mode needs a database name in the PostgreSQL connection string.");
        _templateDatabase = _baseDatabase + SandboxInfix + TemplateSuffix;

        // 32 is the length of a sandbox identifier.
        if (_baseDatabase.Length + SandboxInfix.Length + 32 > MaxIdentifierLength)
        {
            throw new InvalidOperationException(
                $"The database name '{_baseDatabase}' is too long for Demo Mode: sandbox database names must fit into {MaxIdentifierLength} characters.");
        }
    }

    /// <inheritdoc />
    public string GetSandboxDatabaseName(string sandboxId) => _baseDatabase + SandboxInfix + sandboxId;

    /// <inheritdoc />
    public string GetConnectionString(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(_baseBuilder.ConnectionString)
        {
            Database = databaseName,
            MinPoolSize = 0,
            MaxPoolSize = _maxPoolSize,
            ConnectionIdleLifetime = IdleLifetimeSeconds,
            ConnectionPruningInterval = PruningIntervalSeconds,
        };

        return builder.ConnectionString;
    }

    /// <inheritdoc />
    public async Task PrepareTemplateAsync(CancellationToken cancellationToken)
    {
        // The template is copied from the application database, which must have no open connections.
        NpgsqlConnection.ClearAllPools();

        await using var connection = await OpenMaintenanceConnectionAsync(cancellationToken);

        foreach (string leftover in await ListSandboxDatabasesAsync(connection, cancellationToken))
        {
            await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS {Quote(leftover)} WITH (FORCE)", cancellationToken);
            LogLeftoverDropped(_logger, leftover);
        }

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await ExecuteAsync(
                    connection,
                    $"CREATE DATABASE {Quote(_templateDatabase)} TEMPLATE {Quote(_baseDatabase)}",
                    cancellationToken);

                break;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ObjectInUse && attempt < TemplateAttempts)
            {
                // Someone else is still connected to the application database.
                LogTemplateRetry(_logger, attempt, ex.MessageText);
                NpgsqlConnection.ClearAllPools();
                await Task.Delay(TemplateRetryDelay, cancellationToken);
            }
        }

        LogTemplatePrepared(_logger, _templateDatabase, _baseDatabase);
    }

    /// <inheritdoc />
    public async Task CreateSandboxAsync(string databaseName, CancellationToken cancellationToken)
    {
        await using var connection = await OpenMaintenanceConnectionAsync(cancellationToken);

        await ExecuteAsync(
            connection,
            $"CREATE DATABASE {Quote(databaseName)} TEMPLATE {Quote(_templateDatabase)}",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task DropSandboxAsync(string databaseName, CancellationToken cancellationToken)
    {
        await using (var pooled = new NpgsqlConnection(GetConnectionString(databaseName)))
        {
            NpgsqlConnection.ClearPool(pooled);
        }

        await using var connection = await OpenMaintenanceConnectionAsync(cancellationToken);

        await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS {Quote(databaseName)} WITH (FORCE)", cancellationToken);
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        // Database names cannot be passed as parameters; every identifier in the statement goes through Quote().
#pragma warning disable CA2100
        await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<List<string>> ListSandboxDatabasesAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT datname FROM pg_database WHERE starts_with(datname, @prefix)",
            connection);

        command.Parameters.AddWithValue("prefix", _baseDatabase + SandboxInfix);

        var names = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private async Task<NpgsqlConnection> OpenMaintenanceConnectionAsync(CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(_baseBuilder.ConnectionString)
        {
            Database = MaintenanceDatabase,
            Pooling = false,
        };

        var connection = new NpgsqlConnection(builder.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        return connection;
    }

    [LoggerMessage(EventId = DemoLogEventIds.SandboxLeftoverDropped, Level = LogLevel.Information, Message = "Demo mode dropped the leftover database '{Database}'.")]
    private static partial void LogLeftoverDropped(ILogger logger, string database);

    [LoggerMessage(EventId = DemoLogEventIds.SandboxTemplatePrepared, Level = LogLevel.Information, Message = "Demo mode built the template database '{Template}' from '{Source}'.")]
    private static partial void LogTemplatePrepared(ILogger logger, string template, string source);

    [LoggerMessage(EventId = DemoLogEventIds.SandboxTemplateRetry, Level = LogLevel.Warning, Message = "Demo mode could not copy the application database (attempt {Attempt}): {Reason}")]
    private static partial void LogTemplateRetry(ILogger logger, int attempt, string reason);
}
