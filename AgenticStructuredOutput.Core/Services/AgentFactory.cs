using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticStructuredOutput.Services;

public class AgentFactory(IChatClient chatClient, ILogger<AgentFactory> logger) : IAgentFactory
{
    private readonly IChatClient _chatClient = chatClient;
    private readonly ILogger<AgentFactory> _logger = logger;

    public Task<AIAgent> CreateDataMappingAgentAsync(ChatOptions? chatOptions = null)
    {
        // Create AIAgent with full instructions directly
        var agent = _chatClient.CreateAIAgent(new ChatClientAgentOptions
        {
            Name = "DataMappingExpert",
            Instructions = AgentInstructions.DataMappingExpert,
            ChatOptions = chatOptions
        });

        _logger.LogInformation(
            "Created AIAgent '{AgentName}' with data mapping instructions",
            "DataMappingExpert");
        
        return Task.FromResult<AIAgent>(agent);
    }
}