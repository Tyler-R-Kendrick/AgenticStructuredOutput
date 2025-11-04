using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgenticStructuredOutput.Services;

public interface IAgentFactory
{
    Task<AIAgent> CreateDataMappingAgentAsync(ChatOptions? chatOptions = null);
}
