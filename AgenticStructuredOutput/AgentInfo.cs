using System.Text.Json.Serialization;

namespace AgenticStructuredOutput;

public class AgentInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("framework")]
    public string? Framework { get; set; }

    [JsonPropertyName("hosting")]
    public string? Hosting { get; set; }

    [JsonPropertyName("inference")]
    public string? Inference { get; set; }

    [JsonPropertyName("authentication")]
    public string? Authentication { get; set; }

    [JsonPropertyName("endpoints")]
    public string[]? Endpoints { get; set; }

    [JsonPropertyName("features")]
    public string[]? Features { get; set; }
}