using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using A2A.AspNetCore;
using Microsoft.Agents.AI;
using A2A;
using AgenticStructuredOutput.Services;

namespace AgenticStructuredOutput.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds chat client services to the dependency injection container using OpenAI SDK.
    /// Uses the AzureInferenceChatClientBuilder for consistent configuration with GitHub Models.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Optional action to configure inference options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAgentServices(
        this IServiceCollection services,
        Action<AzureAIInferenceOptions>? configureOptions = null)
    {
        services.AddSingleton<IAgentFactory, AgentFactory>();
        services.AddSingleton<IAgentExecutionService, AgentExecutionService>();

        // Configure options with defaults
        AzureAIInferenceOptions options = new()
        {
            ApiKey = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
        };
        configureOptions?.Invoke(options);

        // Resolve API key: explicit config > environment variable > fallback
        var apiKey = options.ApiKey ?? throw new InvalidOperationException(
                $"No API key configured. Set GITHUB_TOKEN environment variable or provide ApiKey in AzureAIInferenceOptions.");

        // Create chat client using builder for consistency
        var chatClient = new AzureInferenceChatClientBuilder()
            .WithApiKey(apiKey)
            .BuildIChatClient();

        // Register chat client as singleton (we'll create agents dynamically per request)
        services.AddSingleton(chatClient);
        return services;
    }

    /// <summary>
    /// Adds chat client services configured from IConfiguration.
    /// Looks for "AzureAIInference" section in configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAgentServices(
        this IServiceCollection services,
        IConfiguration configuration)
        => services.AddAgentServices(options
            => configuration.GetSection("AzureAIInference").Bind(options));
}

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