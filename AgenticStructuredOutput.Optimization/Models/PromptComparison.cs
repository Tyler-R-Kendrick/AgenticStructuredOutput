namespace AgenticStructuredOutput.Optimization.Models;

/// <summary>
/// Comparison between two prompt variants (baseline vs candidate).
/// </summary>
public class PromptComparison
{
    /// <summary>
    /// Whether the candidate prompt is an improvement over baseline.
    /// </summary>
    public bool IsImprovement { get; set; }
    
    /// <summary>
    /// Change in composite score (candidate - baseline).
    /// </summary>
    public double DeltaCompositeScore { get; set; }
    
    /// <summary>
    /// Change in score for each metric.
    /// </summary>
    public Dictionary<string, double> DeltaByMetric { get; set; } = new();
    
    /// <summary>
    /// Metrics that improved.
    /// </summary>
    public List<string> ImprovedMetrics { get; set; } = new();
    
    /// <summary>
    /// Metrics that regressed.
    /// </summary>
    public List<string> RegressedMetrics { get; set; } = new();
    
    /// <summary>
    /// Human-readable summary of the comparison.
    /// </summary>
    public string Summary { get; set; } = string.Empty;
    
    /// <summary>
    /// Baseline metrics.
    /// </summary>
    public AggregatedMetrics Baseline { get; set; } = new();
    
    /// <summary>
    /// Candidate metrics.
    /// </summary>
    public AggregatedMetrics Candidate { get; set; } = new();
}
