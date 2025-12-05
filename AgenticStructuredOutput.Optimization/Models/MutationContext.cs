using System.Text.Json;

namespace AgenticStructuredOutput.Optimization.Models;

/// <summary>
/// Context information provided to mutation strategies.
/// </summary>
public class MutationContext
{
    /// <summary>
    /// Test cases to consider when mutating.
    /// </summary>
    public List<EvalTestCase> TestCases { get; set; } = new();
    
    /// <summary>
    /// Test cases that failed in previous evaluation.
    /// </summary>
    public List<EvalTestCase> FailedTestCases { get; set; } = new();
    
    /// <summary>
    /// Current metrics for the prompt being mutated.
    /// </summary>
    public AggregatedMetrics? CurrentMetrics { get; set; }
    
    /// <summary>
    /// Target metric to improve (e.g., "Grounding", "Correctness").
    /// </summary>
    public string? TargetImprovement { get; set; }
    
    /// <summary>
    /// Schema being used for evaluation.
    /// </summary>
    public JsonElement? Schema { get; set; }
    
    /// <summary>
    /// Random number generator for reproducibility.
    /// </summary>
    public Random Random { get; set; } = Random.Shared;
}
