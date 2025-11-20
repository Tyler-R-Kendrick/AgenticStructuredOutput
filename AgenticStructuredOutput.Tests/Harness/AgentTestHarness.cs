using AgenticStructuredOutput.Services;
using AgenticStructuredOutput.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AgenticStructuredOutput.Tests.Harness;

/// <summary>
/// Common test harness that encapsulates shared setup and agent invocation logic.
/// Derived test classes inherit schema loading, agent creation, and logging capabilities.
/// </summary>
public abstract class AgentTestHarness
{
    protected const int MaxTestTimeoutMs = 30000;  // 30 seconds for LLM API calls
    protected JsonElement? DefaultSchemaElement { get; private set; }

    /// <summary>
    /// Load the default schema from the main project's embedded resources.
    /// Called during [OneTimeSetUp].
    /// </summary>
    [OneTimeSetUp]
    public virtual void SetupDefaultSchema()
    {
        try
        {
            var schemaJson = EmbeddedSchemaLoader.LoadSchemaJson();
            var schemaDoc = JsonDocument.Parse(schemaJson);
            DefaultSchemaElement = schemaDoc.RootElement.Clone();
            
            LogSync($"✓ Schema loaded successfully");
        }
        catch (Exception ex)
        {
            LogSync($"✗ Failed to load schema: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Invokes the data mapping agent with the provided user message and optional schema override.
    /// Handles DI setup, agent creation, and execution internally.
    /// </summary>
    protected async Task<string> InvokeAgentAsync(string userMessage, JsonElement? overrideSchema = null)
    {
        try
        {
            var schemaElement = (overrideSchema ?? DefaultSchemaElement) 
                ?? throw new InvalidOperationException("Schema element is not initialized");

            await LogAsync("Setting up DI container...");
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            services.AddAgentServices();

            var serviceProvider = services.BuildServiceProvider();
            var factory = serviceProvider.GetRequiredService<IAgentFactory>();

            var modelId = Environment.GetEnvironmentVariable("MODEL_ID") ?? "openai/gpt-4o-mini";
            await LogAsync($"Creating agent with model: {modelId}");

            var agent = await factory.CreateDataMappingAgentAsync(new()
            {
                ModelId = modelId,
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    schema: schemaElement,
                    schemaName: "DynamicOutput",
                    schemaDescription: "Intelligently mapped JSON output conforming to the provided schema"
                )
            });

            await LogAsync($"Invoking agent...");
            var response = await agent.RunAsync(userMessage, cancellationToken: TestContext.CurrentContext.CancellationToken);

            return response?.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            await LogAsync($"Error: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
            {
                await LogAsync($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// Async logging to test output. Call from test methods.
    /// </summary>
    protected static async Task LogAsync(string message)
    {
        await TestContext.Out.WriteLineAsync($"[HARNESS] {message}");
    }

    /// <summary>
    /// Synchronous logging to test output. Call from setup/teardown.
    /// </summary>
    protected static void LogSync(string message)
    {
        TestContext.Out.WriteLine($"[HARNESS] {message}");
    }

    /// <summary>
    /// Helper to get the full path to a test resource file in the Resources directory.
    /// </summary>
    protected static string GetTestResourcePath(string fileName)
    {
        return Path.Combine(
            Path.GetDirectoryName(typeof(AgentTestHarness).Assembly.Location) ?? ".",
            "Resources",
            fileName
        );
    }
}
