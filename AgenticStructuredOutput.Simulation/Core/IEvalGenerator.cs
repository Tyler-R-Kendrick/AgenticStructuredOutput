using AgenticStructuredOutput.Simulation.Models;
using System.Text.Json;

namespace AgenticStructuredOutput.Simulation.Core;

/// <summary>
/// Interface for generating evaluation test cases from a prompt and schema.
/// </summary>
public interface IEvalGenerator
{
    /// <summary>
    /// Generates evaluation test cases based on a prompt and schema.
    /// </summary>
    /// <param name="prompt">The prompt to generate test cases for.</param>
    /// <param name="schema">The JSON schema that defines the expected output structure.</param>
    /// <param name="config">Configuration for test case generation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Simulation result containing generated test cases.</returns>
    Task<SimulationResult> GenerateTestCasesAsync(
        string prompt,
        JsonElement schema,
        SimulationConfig config,
        CancellationToken cancellationToken = default);
}
