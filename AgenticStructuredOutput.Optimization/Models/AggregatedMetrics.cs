namespace AgenticStructuredOutput.Optimization.Models;

/// <summary>
/// Aggregated evaluation metrics for a prompt across multiple test cases.
/// </summary>
public class AggregatedMetrics
{
    /// <summary>
    /// Unique identifier for the prompt variant.
    /// </summary>
    public string PromptId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// The prompt text that was evaluated.
    /// </summary>
    public string PromptText { get; set; } = string.Empty;
    
    /// <summary>
    /// Average score for each evaluation metric (e.g., "Relevance": 4.2).
    /// </summary>
    public Dictionary<string, double> AverageScores { get; set; } = new();
    
    /// <summary>
    /// Minimum score observed for each metric.
    /// </summary>
    public Dictionary<string, double> MinScores { get; set; } = new();
    
    /// <summary>
    /// Maximum score observed for each metric.
    /// </summary>
    public Dictionary<string, double> MaxScores { get; set; } = new();
    
    /// <summary>
    /// Standard deviation for each metric.
    /// </summary>
    public Dictionary<string, double> StdDeviation { get; set; } = new();
    
    /// <summary>
    /// Number of test cases evaluated.
    /// </summary>
    public int TestCaseCount { get; set; }
    
    /// <summary>
    /// Weighted composite score across all metrics.
    /// </summary>
    public double CompositeScore { get; set; }
    
    /// <summary>
    /// Number of test cases that passed (score >= 3.0).
    /// </summary>
    public int PassedTestCases { get; set; }
    
    /// <summary>
    /// Pass rate (percentage of test cases that passed).
    /// </summary>
    public double PassRate => TestCaseCount > 0 
        ? (double)PassedTestCases / TestCaseCount 
        : 0.0;
    
    /// <summary>
    /// Timestamp when evaluation was performed.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
