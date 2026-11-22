using Crm.Cli;
using Crm.Cli.Extensions;
using Crm.Cli.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

HashSet<string> knownBooleanFlags = new(StringComparer.OrdinalIgnoreCase)
{
    "help",
    "h",
    "password-stdin",
};

var parsedArgs = CommandLineParser.Parse(args, knownBooleanFlags);

var configuration = new ConfigurationBuilder()
    .AddCliConfiguration()
    .Build();

await using var serviceProvider = new ServiceCollection()
    .AddCliServices(configuration)
    .BuildServiceProvider();

await using var scope = serviceProvider.CreateAsyncScope();
var runner = scope.ServiceProvider.GetRequiredService<CliRunner>();

return await runner.RunAsync(parsedArgs);
