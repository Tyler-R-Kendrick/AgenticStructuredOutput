using AgenticStructuredOutput.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using System.Linq;

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
        => services.AddAgentServicesInternal(configuration: null, configureOptions);

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
        => services.AddAgentServicesInternal(configuration, configureOptions: null);

    public static IServiceCollection AddAgentServices(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AzureAIInferenceOptions>? configureOptions)
        => services.AddAgentServicesInternal(configuration, configureOptions);

    private static IServiceCollection AddAgentServicesInternal(
        this IServiceCollection services,
        IConfiguration? configuration,
        Action<AzureAIInferenceOptions>? configureOptions)
    {
        services.AddSingleton<IAgentFactory, AgentFactory>();
        services.AddSingleton<IAgentExecutionService, AgentExecutionService>();

        services.AddSingleton<IChatClient>(provider =>
        {
            AzureAIInferenceOptions options = new();

            // Bind from configuration sources (appsettings, user secrets, environment variables)
            var config = configuration ?? provider.GetService<IConfiguration>();
            config?.GetSection("AzureAIInference").Bind(options);

            // Allow explicit overrides via delegate
            configureOptions?.Invoke(options);

            var apiKey = ResolveApiKey(options);

            var builder = new AzureInferenceChatClientBuilder()
                .WithApiKey(apiKey)
                .WithModelId(options.ModelId)
                .WithEndpoint(options.Endpoint);

            return builder.BuildIChatClient();
        });

        return services;
    }

    private static string ResolveApiKey(AzureAIInferenceOptions options)
    {
        var apiKey = new[]
            {
                options.ApiKey,
                Environment.GetEnvironmentVariable("GITHUB_TOKEN"),
                Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            }
            .Select(value => value?.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "No API key configured. Provide AzureAIInference:ApiKey via appsettings/user secrets or set GITHUB_TOKEN / OPENAI_API_KEY environment variables.");
        }

        return apiKey!;
    }
}
