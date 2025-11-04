using A2A.AspNetCore;
using AgenticStructuredOutput.Services;

namespace AgenticStructuredOutput.Extensions;

/// <summary>
/// Extension methods for configuring A2A routing and agent endpoints.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures the A2A (Agent-to-Agent) routing for the agent service.
    /// </summary>
    /// <param name="app">The web application</param>
    /// <param name="executionService">The agent execution service</param>
    /// <returns>The web application for chaining</returns>
    public static WebApplication MapAgentRoutes(this WebApplication app, IAgentExecutionService executionService)
    {
        if (executionService.Agent == null)
        {
            throw new InvalidOperationException("Agent execution service must be initialized before mapping routes");
        }

        app.MapA2A(
            executionService.Agent,
            path: "/",
            agentCard: executionService.AgentCard,
            taskManager => app.MapWellKnownAgentCard(taskManager, "/"));

        return app;
    }
}