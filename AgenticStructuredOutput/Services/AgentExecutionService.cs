using System.Text.Json;
using Json.Schema;
using Json.More;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using A2A;
using AgenticStructuredOutput.Models;

namespace AgenticStructuredOutput.Services;

/// <summary>
/// Business logic layer for agent execution and request/response handling.
/// Encapsulates all concerns related to processing agent requests.
/// </summary>
public interface IAgentExecutionService
{
    /// <summary>
    /// Initializes the agent with the default embedded schema at startup.
    /// Should be called once during application initialization.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Gets the initialized agent instance.
    /// </summary>
    AIAgent? Agent { get; }

    /// <summary>
    /// Gets the A2A agent card metadata.
    /// </summary>
    AgentCard AgentCard { get; }
}

public class AgentExecutionService(IAgentFactory agentFactory, ILogger<AgentExecutionService> logger) : IAgentExecutionService
{
    private readonly IAgentFactory _agentFactory = agentFactory;
    private readonly ILogger<AgentExecutionService> _logger = logger;
    private AIAgent? _agent;
    private readonly AgentCard _agentCard = new()
    {
        Name = "Agentic Structured Output Agent",
        Description = "An AI agent that produces structured JSON output based on a provided schema.",
        IconUrl = "https://example.com/agent-icon.png"
    };

    public AIAgent? Agent => _agent;

    public AgentCard AgentCard => _agentCard;

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing agent execution service");

        // Load schema from embedded resource
        var schemaJson = LoadEmbeddedSchema();
        var jsonSchema = JsonSchema.FromText(schemaJson);
        var schemaJsonElement = jsonSchema.ToJsonDocument().RootElement;

        // Create the AI agent with the embedded schema
        _agent = await _agentFactory.CreateDataMappingAgentAsync(new()
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                schema: schemaJsonElement,
                schemaName: "DynamicOutput",
                schemaDescription: "Intelligently mapped JSON output conforming to the provided schema"
            )
        });

        _logger.LogInformation("Agent initialization complete");
    }

    /// <summary>
    /// Loads the default schema from embedded resources.
    /// </summary>
    private static string LoadEmbeddedSchema()
    {
        var assembly = typeof(AgentExecutionService).Assembly;
        var resourceName = "AgenticStructuredOutput.Resources.schema.json";
        
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not find embedded resource: {resourceName}");
        
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
