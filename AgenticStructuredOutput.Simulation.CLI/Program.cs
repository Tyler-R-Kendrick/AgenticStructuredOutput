using AgenticStructuredOutput.Extensions;
using AgenticStructuredOutput.Simulation;
using AgenticStructuredOutput.Simulation.Core;
using AgenticStructuredOutput.Simulation.CLI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = CreateHost(args);
using var scope = host.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

logger.LogInformation("╔═══════════════════════════════════════════════════════════╗");
logger.LogInformation("║ AgenticStructuredOutput - Simulation CLI                 ║");
logger.LogInformation("╚═══════════════════════════════════════════════════════════╝");
logger.LogInformation(string.Empty);

try
{
    var runner = scope.ServiceProvider.GetRequiredService<SimulationRunner>();
    return await runner.RunAsync(args);
}
catch (Exception ex)
{
    logger.LogError(ex, "Simulation run failed: {Message}", ex.Message);
    return 1;
}

static IHost CreateHost(string[] args)
{
    return Host.CreateDefaultBuilder(args)
        .ConfigureServices((_, services) =>
        {
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            services.AddAgentServices();
            services.AddSingleton<IEvalGenerator, LlmEvalGenerator>();
            services.AddSingleton<IEvalPersistence, JsonLEvalPersistence>();
            services.AddSingleton<SimulationRunner>();
        })
        .Build();
}
