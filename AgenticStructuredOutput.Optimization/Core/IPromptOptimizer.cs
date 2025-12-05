using AgenticStructuredOutput.Optimization.Models;

namespace AgenticStructuredOutput.Optimization.Core;

/// <summary>
/// Interface for prompt optimization.
/// </summary>
public interface IPromptOptimizer
{
    /// <summary>
    /// Optimize a prompt to improve its performance on the given test cases.
    /// </summary>
    /// <param name="baselinePrompt">The starting prompt to optimize.</param>
    /// <param name="testCases">Test cases to optimize against.</param>
    /// <param name="config">Optimization configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Optimization result containing the best prompt and metrics.</returns>
    Task<OptimizationResult> OptimizeAsync(
        string baselinePrompt,
        IEnumerable<EvalTestCase> testCases,
        OptimizationConfig config,
        CancellationToken cancellationToken = default);
}
