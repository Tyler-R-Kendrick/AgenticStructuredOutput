namespace AgenticStructuredOutput.Optimization.Models;

/// <summary>
/// Configuration for the optimization process.
/// </summary>
public class OptimizationConfig
{
    /// <summary>
    /// Maximum number of optimization iterations.
    /// </summary>
    public int MaxIterations { get; set; } = 10;
    
    /// <summary>
    /// Minimum improvement required to accept a new prompt (e.g., 0.05 = 5% improvement).
    /// </summary>
    public double ImprovementThreshold { get; set; } = 0.05;
    
    /// <summary>
    /// Threshold for accepting lateral moves (small regressions).
    /// </summary>
    public double LateralMoveThreshold { get; set; } = -0.02;
    
    /// <summary>
    /// Probability of accepting lateral moves (0.0 - 1.0).
    /// </summary>
    public double LateralMoveProbability { get; set; } = 0.1;
    
    /// <summary>
    /// Number of iterations without improvement before triggering random restart.
    /// </summary>
    public int RestartAfterStuckIterations { get; set; } = 3;
    
    /// <summary>
    /// Score threshold for early stopping (if reached, optimization stops).
    /// </summary>
    public double EarlyStoppingScore { get; set; } = 4.5;
    
    /// <summary>
    /// Names of mutation strategies to enable.
    /// </summary>
    public List<string> EnabledStrategies { get; set; } = new()
    {
        "AddExamples",
        "AddConstraints",
        "RephraseInstructions",
        "SimplifyLanguage"
    };
    
    /// <summary>
    /// Weights for each evaluation metric when computing composite score.
    /// Higher weight = more important.
    /// </summary>
    public Dictionary<string, double> MetricWeights { get; set; } = new()
    {
        { "Relevance", 1.0 },
        { "Correctness", 1.5 },      // Slightly higher weight
        { "Completeness", 1.0 },
        { "Grounding", 1.25 }
    };
    
    /// <summary>
    /// Whether to run evaluations in parallel.
    /// </summary>
    public bool ParallelEvaluation { get; set; } = true;
    
    /// <summary>
    /// Maximum number of parallel evaluation tasks.
    /// </summary>
    public int MaxParallelTasks { get; set; } = 4;
}
