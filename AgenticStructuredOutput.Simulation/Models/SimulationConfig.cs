namespace AgenticStructuredOutput.Simulation.Models;

/// <summary>
/// Configuration for eval generation simulation.
/// </summary>
public class SimulationConfig
{
    /// <summary>
    /// Number of eval test cases to generate.
    /// </summary>
    public int TestCaseCount { get; set; } = 10;
    
    /// <summary>
    /// Types of evaluations to generate (e.g., "Relevance", "Correctness", "Completeness", "Grounding").
    /// </summary>
    public List<string> EvaluationTypes { get; set; } = new() 
    { 
        "Relevance", 
        "Correctness", 
        "Completeness", 
        "Grounding" 
    };
    
    /// <summary>
    /// Diversity factor for generated test cases (0.0 to 1.0).
    /// Higher values generate more diverse scenarios.
    /// </summary>
    public double DiversityFactor { get; set; } = 0.7;
    
    /// <summary>
    /// Temperature for LLM generation (0.0 to 2.0).
    /// </summary>
    public float Temperature { get; set; } = 0.8f;
    
    /// <summary>
    /// Whether to include edge cases in generated test cases.
    /// </summary>
    public bool IncludeEdgeCases { get; set; } = true;
}
