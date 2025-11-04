using System.Net.Http.Json;
using System.Text.Json;

namespace AgenticStructuredOutput.Services;

/// <summary>
/// Client for interacting with Langfuse REST API to fetch prompts by name and label.
/// </summary>
public sealed class LangfuseClient
{
    private readonly HttpClient _http;
    private readonly ILogger<LangfuseClient> _logger;
    private readonly string? _publicKey;
    private readonly string? _secretKey;

    public LangfuseClient(
        HttpClient http, 
        ILogger<LangfuseClient> logger,
        string? publicKey = null, 
        string? secretKey = null)
    {
        _http = http;
        _logger = logger;
        _publicKey = publicKey;
        _secretKey = secretKey;
    }

    /// <summary>
    /// Retrieves a prompt from Langfuse by name and label.
    /// </summary>
    /// <param name="name">The prompt name to fetch</param>
    /// <param name="label">The label to filter by (e.g., "production", "staging")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The resolved prompt text and optional configuration</returns>
    public async Task<(string ResolvedText, string? Config)> GetPromptAsync(
        string name, 
        string label = "production", 
        CancellationToken ct = default)
    {
        try
        {
            var encodedName = Uri.EscapeDataString(name);
            var encodedLabel = Uri.EscapeDataString(label);
            var url = $"/api/public/v2/prompts/{encodedName}?label={encodedLabel}";
            
            _logger.LogInformation("Fetching prompt '{Name}' with label '{Label}' from Langfuse", name, label);

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            
            // Add authentication headers if keys are provided
            if (!string.IsNullOrEmpty(_publicKey) && !string.IsNullOrEmpty(_secretKey))
            {
                // Langfuse uses basic auth with public key as username and secret key as password
                var authValue = Convert.ToBase64String(
                    System.Text.Encoding.ASCII.GetBytes($"{_publicKey}:{_secretKey}"));
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
            }

            var res = await _http.SendAsync(req, ct);
            
            if (!res.IsSuccessStatusCode)
            {
                var errorContent = await res.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to fetch prompt from Langfuse. Status: {StatusCode}, Error: {Error}", 
                    res.StatusCode, errorContent);
                res.EnsureSuccessStatusCode();
            }

            var json = await res.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
            
            if (json?.RootElement.TryGetProperty("prompt", out var promptProp) != true)
            {
                throw new InvalidOperationException("Response does not contain 'prompt' property");
            }

            // Extract prompt text (may be string or complex object)
            string resolvedText;
            if (promptProp.ValueKind == JsonValueKind.String)
            {
                resolvedText = promptProp.GetString() ?? string.Empty;
            }
            else
            {
                // For chat-style prompts or complex structures, serialize to JSON
                resolvedText = JsonSerializer.Serialize(promptProp);
            }

            // Extract config if present
            string? config = null;
            if (json.RootElement.TryGetProperty("config", out var configProp))
            {
                config = JsonSerializer.Serialize(configProp);
            }

            _logger.LogInformation("Successfully fetched prompt '{Name}' (version: {Version})", 
                name, 
                json.RootElement.TryGetProperty("version", out var ver) ? ver.ToString() : "unknown");

            return (resolvedText, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching prompt '{Name}' with label '{Label}' from Langfuse", name, label);
            throw;
        }
    }

    /// <summary>
    /// Gets prompt with caching support to minimize API calls.
    /// </summary>
    public async Task<string> GetPromptTextAsync(
        string name, 
        string label = "production", 
        CancellationToken ct = default)
    {
        var (text, _) = await GetPromptAsync(name, label, ct);
        return text;
    }
}
