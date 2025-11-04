using AgenticStructuredOutput.Optimization.Models;

namespace AgenticStructuredOutput.Simulation.Core;

/// <summary>
/// Interface for persisting evaluation test cases to JSONL files.
/// </summary>
public interface IEvalPersistence
{
    /// <summary>
    /// Saves evaluation test cases to a JSONL file.
    /// Each test case is written as a single JSON object per line.
    /// </summary>
    /// <param name="testCases">Test cases to save.</param>
    /// <param name="filePath">Path to the JSONL file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveTestCasesAsync(
        IEnumerable<EvalTestCase> testCases,
        string filePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Loads evaluation test cases from a JSONL file.
    /// </summary>
    /// <param name="filePath">Path to the JSONL file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Loaded test cases.</returns>
    Task<List<EvalTestCase>> LoadTestCasesAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
