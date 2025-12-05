namespace AgenticStructuredOutput.Optimization.Models;

/// <summary>
/// Result of an optimization run.
/// </summary>
public class OptimizationResult
{
    /// <summary>
    /// The best prompt found during optimization.
    /// </summary>
    public string BestPrompt { get; set; } = string.Empty;
    
    /// <summary>
    /// Metrics for the best prompt.
    /// </summary>
    public AggregatedMetrics BestMetrics { get; set; } = new();
    
    /// <summary>
    /// Metrics for the baseline (starting) prompt.
    /// </summary>
    public AggregatedMetrics BaselineMetrics { get; set; } = new();
    
    /// <summary>
    /// History of all optimization iterations.
    /// </summary>
    public List<OptimizationIteration> History { get; set; } = new();
    
    /// <summary>
    /// Total duration of the optimization run.
    /// </summary>
    public TimeSpan Duration { get; set; }
    
    /// <summary>
    /// Total improvement achieved (best - baseline composite score).
    /// </summary>
    public double TotalImprovement => BestMetrics.CompositeScore - BaselineMetrics.CompositeScore;
    
    /// <summary>
    /// Whether optimization improved the prompt.
    /// </summary>
    public bool DidImprove => TotalImprovement > 0;
    
    /// <summary>
    /// Reason for optimization stopping.
    /// </summary>
    public string StoppingReason { get; set; } = string.Empty;
    
    /// <summary>
    /// Configuration used for optimization.
    /// </summary>
    public OptimizationConfig Config { get; set; } = new();
    
    /// <summary>
    /// Timestamp when optimization started.
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// Timestamp when optimization completed.
    /// </summary>
    public DateTime EndTime { get; set; }
}
