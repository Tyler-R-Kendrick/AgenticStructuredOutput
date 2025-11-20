using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace AgenticStructuredOutput.Extensions;

/// <summary>
/// Fluent builder for creating and configuring OpenAI ChatClient instances targeting GitHub Models.
/// </summary>
public class AzureInferenceChatClientBuilder
{
    private string? _apiKey;
    private string _modelId = "openai/gpt-4o-mini";
    private string _endpoint = "https://models.github.ai/inference";

    /// <summary>
    /// Overrides the default model identifier.
    /// </summary>
    public AzureInferenceChatClientBuilder WithModelId(string? modelId)
    {
        if (!string.IsNullOrWhiteSpace(modelId))
        {
            _modelId = modelId;
        }

        return this;
    }

    /// <summary>
    /// Overrides the default inference endpoint.
    /// </summary>
    public AzureInferenceChatClientBuilder WithEndpoint(string? endpoint)
    {
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            _endpoint = endpoint;
        }

        return this;
    }

    /// <summary>
    /// Loads the API key from environment variables with fallback options.
    /// Tries in order: GITHUB_TOKEN, OPENAI_API_KEY, or provided fallback.
    /// </summary>
    public AzureInferenceChatClientBuilder WithApiKey(string? fallbackKey = "GITHUB_TOKEN")
    {
        _apiKey = fallbackKey
            ?? throw new InvalidOperationException(
                "No API key found in GITHUB_TOKEN or OPENAI_API_KEY environment variables, and no fallback provided");
        return this;
    }

    /// <summary>
    /// Uses the GitHub Models endpoint explicitly.
    /// </summary>
    public AzureInferenceChatClientBuilder UseGitHubModelsEndpoint()
    {
        _endpoint = "https://models.github.ai/inference";
        return this;
    }

    /// <summary>
    /// Validates the current configuration before building.
    /// </summary>
    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException(
                "API key is required. Use WithApiKey(), WithEnvironmentApiKey(), or WithEnvironmentApiKeyOrDefault()");
        }

        if (string.IsNullOrWhiteSpace(_modelId))
        {
            throw new InvalidOperationException("Model ID is required before building the chat client.");
        }

        if (string.IsNullOrWhiteSpace(_endpoint))
        {
            throw new InvalidOperationException("Endpoint is required before building the chat client.");
        }
    }

    /// <summary>
    /// Builds and returns an IChatClient with the configured settings.
    /// </summary>
    public IChatClient BuildIChatClient()
    {
        Validate();

        var apiKeyCredential = new ApiKeyCredential(_apiKey!);
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(_endpoint),
        };

        var openAiClient = new OpenAIClient(apiKeyCredential, clientOptions);
        var chatClient = openAiClient.GetChatClient(_modelId);
        return chatClient.AsIChatClient();
    }
}
