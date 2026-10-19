using Crm.Cli;
using Crm.Cli.Extensions;
using Crm.Cli.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var parsedArgs = CommandLineParser.Parse(args);

var configuration = new ConfigurationBuilder()
    .AddCliConfiguration()
    .Build();

using var serviceProvider = new ServiceCollection()
    .AddCliServices(configuration)
    .BuildServiceProvider();

using var scope = serviceProvider.CreateScope();
var runner = scope.ServiceProvider.GetRequiredService<CliRunner>();

return await runner.RunAsync(parsedArgs);
