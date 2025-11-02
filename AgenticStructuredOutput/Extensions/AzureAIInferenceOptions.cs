namespace AgenticStructuredOutput.Extensions;

/// <summary>
/// Configuration for Azure AI Inference chat client setup.
/// </summary>
public class AzureAIInferenceOptions
{
    /// <summary>
    /// The API key to use. Defaults to GITHUB_TOKEN environment variable.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The endpoint URI for the service. Defaults to GitHub Models endpoint.
    /// </summary>
    public string Endpoint { get; set; } = "https://models.github.ai/inference";

    /// <summary>
    /// The model ID to use for inference. Defaults to "openai/gpt-4o".
    /// </summary>
    public string ModelId { get; set; } = "openai/gpt-4o";
}
