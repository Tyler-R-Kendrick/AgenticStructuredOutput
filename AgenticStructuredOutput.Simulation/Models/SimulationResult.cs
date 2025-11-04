using AgenticStructuredOutput.Optimization.Models;

namespace AgenticStructuredOutput.Simulation.Models;

/// <summary>
/// Result of a simulation run that generated evaluation test cases.
/// </summary>
public class SimulationResult
{
    /// <summary>
    /// Generated evaluation test cases.
    /// </summary>
    public List<EvalTestCase> TestCases { get; set; } = new();
    
    /// <summary>
    /// Total time taken to generate test cases.
    /// </summary>
    public TimeSpan Duration { get; set; }
    
    /// <summary>
    /// Number of test cases successfully generated.
    /// </summary>
    public int SuccessCount => TestCases.Count;
    
    /// <summary>
    /// Configuration used for simulation.
    /// </summary>
    public SimulationConfig Config { get; set; } = new();
    
    /// <summary>
    /// Timestamp when simulation started.
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// Timestamp when simulation completed.
    /// </summary>
    public DateTime EndTime { get; set; }
    
    /// <summary>
    /// Prompt that was used to generate test cases.
    /// </summary>
    public string SourcePrompt { get; set; } = string.Empty;
}
