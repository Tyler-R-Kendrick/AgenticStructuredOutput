using AgenticStructuredOutput.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;

namespace AgenticStructuredOutput.CLI;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var host = CreateHost(args);
        using var scope = host.Services.CreateScope();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("MappingCLI");

        logger.LogInformation("╔═══════════════════════════════════════════════════════════╗");
        logger.LogInformation("║ AgenticStructuredOutput - Mapping CLI                    ║");
        logger.LogInformation("╚═══════════════════════════════════════════════════════════╝");
        logger.LogInformation(string.Empty);

        try
        {
            var runner = scope.ServiceProvider.GetRequiredService<SchemaMappingRunner>();
            return await runner.RunAsync(args);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Mapping run failed: {Message}", ex.Message);
            return 1;
        }
    }

    private static IHost CreateHost(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureServices((_, services) =>
            {
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                services.AddSingleton<IFileProvider>(_ => CreateSchemaFileProvider());
                services.AddAgentServices();
                services.AddSingleton<SchemaMappingRunner>();
            })
            .Build();
    }

    private static IFileProvider CreateSchemaFileProvider()
    {
        var root = Path.GetPathRoot(Path.GetFullPath("."));
        if (string.IsNullOrEmpty(root))
        {
            root = Directory.GetCurrentDirectory();
        }

        return new PhysicalFileProvider(root);
    }
}
