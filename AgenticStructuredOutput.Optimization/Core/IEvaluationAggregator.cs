using AgenticStructuredOutput.Optimization.Models;

namespace AgenticStructuredOutput.Optimization.Core;

/// <summary>
/// Interface for evaluating and aggregating prompt quality metrics.
/// </summary>
public interface IEvaluationAggregator
{
    /// <summary>
    /// Evaluate a prompt across multiple test cases and aggregate the results.
    /// </summary>
    /// <param name="prompt">The prompt to evaluate.</param>
    /// <param name="testCases">Test cases to evaluate against.</param>
    /// <param name="config">Optimization configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated metrics for the prompt.</returns>
    Task<AggregatedMetrics> EvaluatePromptAsync(
        string prompt,
        IEnumerable<EvalTestCase> testCases,
        OptimizationConfig config,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Compare two prompts and determine if the candidate is an improvement.
    /// </summary>
    /// <param name="baseline">Baseline metrics.</param>
    /// <param name="candidate">Candidate metrics.</param>
    /// <param name="config">Optimization configuration.</param>
    /// <returns>Comparison result.</returns>
    PromptComparison ComparePrompts(
        AggregatedMetrics baseline,
        AggregatedMetrics candidate,
        OptimizationConfig config);
}
