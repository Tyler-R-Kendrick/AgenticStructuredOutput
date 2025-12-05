using AgenticStructuredOutput.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddSingleton(provider =>
        {
            AzureAIInferenceOptions options = new();

            // Bind from configuration sources (appsettings, user secrets, environment variables)
            var config = configuration ?? provider.GetService<IConfiguration>();
            config?.GetSection("AzureAIInference").Bind(options);

            // Allow explicit overrides via delegate
            configureOptions?.Invoke(options);

            EnsureApiKey(options);

            var builder = new AzureInferenceChatClientBuilder(options);
            return builder.BuildIChatClient();
        });

        return services;
    }

    private static void EnsureApiKey(AzureAIInferenceOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            options.ApiKey = options.ApiKey.Trim();
            return;
        }

        var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(githubToken))
        {
            options.ApiKey = githubToken.Trim();
            return;
        }

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openAiKey))
        {
            options.ApiKey = openAiKey.Trim();
            return;
        }

        throw new InvalidOperationException(
            "No API key configured. Provide AzureAIInference:ApiKey via appsettings/user secrets or set GITHUB_TOKEN / OPENAI_API_KEY environment variables.");
    }
}
