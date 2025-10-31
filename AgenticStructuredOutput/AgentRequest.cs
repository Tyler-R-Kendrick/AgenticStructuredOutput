using System.Text.Json.Serialization;

namespace AgenticStructuredOutput;

// Request model with dynamic schema
public class AgentRequest
{
    [JsonPropertyName("input")]
    public string? Input { get; set; }

    [JsonPropertyName("schema")]
    public string? Schema { get; set; }
}