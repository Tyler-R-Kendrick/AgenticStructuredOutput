using Microsoft.Agents.AI;
using A2A;

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
