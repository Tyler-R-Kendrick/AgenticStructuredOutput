using OpenAI;
using Microsoft.Extensions.AI;
using System.ClientModel;

namespace AgenticStructuredOutput.Extensions;

/// <summary>
/// Fluent builder for creating and configuring OpenAI ChatClient instances.
/// Provides a clean API for test and application initialization patterns with GitHub Models.
/// 
/// Note: Named AzureInferenceChatClientBuilder to avoid naming conflicts with Microsoft.Extensions.AI.ChatClientBuilder.
/// </summary>
public class AzureInferenceChatClientBuilder
{
    private string? _apiKey;
    private string? _modelId = "openai/gpt-4o-mini";

    /// <summary>
    /// Sets the API key directly (useful for tests with known tokens).
    /// </summary>
    /// <param name="apiKey">The API key to use</param>
    /// <returns>This builder for method chaining</returns>
    public AzureInferenceChatClientBuilder WithApiKey(string apiKey)
    {
        _apiKey = apiKey;
        return this;
    }

    /// <summary>
    /// Loads the API key from environment variables with fallback options.
    /// Tries in order: GITHUB_TOKEN, OPENAI_API_KEY, or provided fallback.
    /// </summary>
    /// <param name="fallbackKey">Optional fallback key if environment variables are not set</param>
    /// <returns>This builder for method chaining</returns>
    public AzureInferenceChatClientBuilder WithEnvironmentApiKey(string? fallbackKey = null)
    {
        _apiKey = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? fallbackKey
            ?? throw new InvalidOperationException(
                "No API key found in GITHUB_TOKEN or OPENAI_API_KEY environment variables, and no fallback provided");
        return this;
    }

    /// <summary>
    /// Loads the API key from environment variables with a default fallback.
    /// Useful for tests where a placeholder is acceptable.
    /// </summary>
    /// <returns>This builder for method chaining</returns>
    public AzureInferenceChatClientBuilder WithEnvironmentApiKeyOrDefault()
    {
        _apiKey = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? "test-placeholder-key";
        return this;
    }

    /// <summary>
    /// Uses the GitHub Models endpoint explicitly.
    /// </summary>
    /// <returns>This builder for method chaining</returns>
    public AzureInferenceChatClientBuilder UseGitHubModelsEndpoint()
    {
        // GitHub Models endpoint is default for OpenAI SDK
        return this;
    }

    /// <summary>
    /// Validates the current configuration before building.
    /// </summary>
    /// <exception cref="InvalidOperationException">If required configuration is missing</exception>
    private void Validate()
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new InvalidOperationException(
                "API key is required. Use WithApiKey(), WithEnvironmentApiKey(), or WithEnvironmentApiKeyOrDefault()");
        }
    }

    /// <summary>
    /// Builds and returns an IChatClient with the configured settings.
    /// This is the recommended pattern for use with Microsoft.Extensions.AI.
    /// </summary>
    /// <returns>A configured IChatClient instance</returns>
    /// <exception cref="InvalidOperationException">If configuration is incomplete</exception>
    public IChatClient BuildIChatClient()
    {
        Validate();
        // Configure for GitHub Models endpoint
        var apiKeyCredential = new ApiKeyCredential(_apiKey!);
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://models.github.ai/inference"),
        };
        var openAiClient = new OpenAIClient(apiKeyCredential, clientOptions);
        var chatClient = openAiClient.GetChatClient(_modelId ?? "openai/gpt-4o-mini");
        // Wrap the OpenAI ChatClient with an adapter that implements IChatClient
        return chatClient.AsIChatClient();
    }

    /// <summary>
    /// Creates a builder for the GitHub Models endpoint with environment token.
    /// This is the recommended pattern for production use.
    /// </summary>
    /// <returns>A new builder configured for GitHub Models</returns>
    public static AzureInferenceChatClientBuilder CreateGitHubModelsClient()
    {
        return new AzureInferenceChatClientBuilder()
            .UseGitHubModelsEndpoint()
            .WithEnvironmentApiKey();
    }

    /// <summary>
    /// Creates a builder for testing scenarios where a placeholder token is acceptable.
    /// </summary>
    /// <returns>A new builder configured with a test placeholder</returns>
    public static AzureInferenceChatClientBuilder CreateForTesting()
    {
        return new AzureInferenceChatClientBuilder()
            .UseGitHubModelsEndpoint()
            .WithEnvironmentApiKeyOrDefault();
    }
}
