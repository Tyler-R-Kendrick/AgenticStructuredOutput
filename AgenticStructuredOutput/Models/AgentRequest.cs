using System.Text.Json.Serialization;

namespace AgenticStructuredOutput.Models;

/// <summary>
/// Request model with dynamic schema for the agent API.
/// </summary>
public class AgentRequest
{
    [JsonPropertyName("input")]
    public string? Input { get; set; }

    [JsonPropertyName("schema")]
    public string? Schema { get; set; }
}
