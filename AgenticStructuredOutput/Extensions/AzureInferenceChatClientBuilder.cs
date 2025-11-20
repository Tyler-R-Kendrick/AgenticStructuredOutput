using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace AgenticStructuredOutput.Extensions;

/// <summary>
/// Builds Microsoft.Extensions.AI chat clients configured for the GitHub Models endpoint.
/// Consumes a fully populated <see cref="AzureAIInferenceOptions"/> instance instead of overriding configuration manually.
/// </summary>
public sealed class AzureInferenceChatClientBuilder(AzureAIInferenceOptions options)
{
    private readonly AzureAIInferenceOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Creates an <see cref="IChatClient"/> using the supplied options.
    /// </summary>
    public IChatClient BuildIChatClient()
    {
        Validate();

        var apiKeyCredential = new ApiKeyCredential(_options.ApiKey!);
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(_options.Endpoint)
        };

        var openAiClient = new OpenAIClient(apiKeyCredential, clientOptions);
        var chatClient = openAiClient.GetChatClient(_options.ModelId);
        return chatClient.AsIChatClient();
    }

    /// <summary>
    /// Creates a builder using API key information sourced from environment variables.
    /// Useful for integration tests and CLI tools that are not using DI configuration binding.
    /// </summary>
    public static AzureInferenceChatClientBuilder CreateFromEnvironment(string? modelId = null)
    {
        var options = CreateOptionsFromEnvironment(modelId);
        return new AzureInferenceChatClientBuilder(options);
    }

    /// <summary>
    /// Creates options using the standard environment fallback chain.
    /// </summary>
    public static AzureAIInferenceOptions CreateOptionsFromEnvironment(string? modelId = null)
    {
        var options = new AzureAIInferenceOptions
        {
            ModelId = string.IsNullOrWhiteSpace(modelId) ? "openai/gpt-4o-mini" : modelId,
            ApiKey = Environment.GetEnvironmentVariable("GITHUB_TOKEN")?.Trim()
        };
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            options.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")?.Trim();
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("Environment variables GITHUB_TOKEN or OPENAI_API_KEY must be set to create chat client options.");
        }

        return options;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("API key is required to build the chat client.");
        }

        if (string.IsNullOrWhiteSpace(_options.ModelId))
        {
            throw new InvalidOperationException("Model ID is required before building the chat client.");
        }

        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            throw new InvalidOperationException("Endpoint is required before building the chat client.");
        }
    }
}
