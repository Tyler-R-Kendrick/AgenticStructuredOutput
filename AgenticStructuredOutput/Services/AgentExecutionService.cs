using Json.Schema;
using Json.More;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using A2A;

namespace AgenticStructuredOutput.Services;

public class AgentExecutionService(
    IAgentFactory agentFactory,
    ILogger<AgentExecutionService> logger)
    : IAgentExecutionService
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
        var schemaJson = EmbeddedSchemaLoader.LoadSchemaJson();
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
}
