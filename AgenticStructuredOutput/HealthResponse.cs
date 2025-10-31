using System.Text.Json.Serialization;

namespace AgenticStructuredOutput;

// Response models
public class HealthResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("agent")]
    public string? Agent { get; set; }

    [JsonPropertyName("framework")]
    public string? Framework { get; set; }

    [JsonPropertyName("inference")]
    public string? Inference { get; set; }

    [JsonPropertyName("authentication")]
    public string? Authentication { get; set; }
}