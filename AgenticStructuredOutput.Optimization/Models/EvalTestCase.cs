using System.Text.Json;

namespace AgenticStructuredOutput.Optimization.Models;

/// <summary>
/// Represents a test case for evaluating prompt quality.
/// </summary>
public class EvalTestCase
{
    /// <summary>
    /// Unique identifier for this test case.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// The type of evaluation (e.g., "Relevance", "Correctness").
    /// </summary>
    public string EvaluationType { get; set; } = string.Empty;
    
    /// <summary>
    /// Human-readable description of the test scenario.
    /// </summary>
    public string TestScenario { get; set; } = string.Empty;
    
    /// <summary>
    /// Input JSON string to be mapped.
    /// </summary>
    public string Input { get; set; } = string.Empty;
    
    /// <summary>
    /// Expected output JSON string (optional, for reference).
    /// </summary>
    public string? ExpectedOutput { get; set; }
    
    /// <summary>
    /// Schema override (optional). If null, uses default schema.
    /// </summary>
    public JsonElement? Schema { get; set; }
}
