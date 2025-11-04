using AgenticStructuredOutput.Optimization.Models;

namespace AgenticStructuredOutput.Optimization.Core;

/// <summary>
/// Interface for prompt mutation strategies.
/// Strategies generate variant prompts by applying specific transformations.
/// </summary>
public interface IPromptMutationStrategy
{
    /// <summary>
    /// Name of the strategy.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description of what this strategy does.
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Apply the mutation strategy to generate a variant prompt.
    /// </summary>
    /// <param name="basePrompt">The prompt to mutate.</param>
    /// <param name="context">Context information for the mutation.</param>
    /// <returns>The mutated prompt.</returns>
    Task<string> MutateAsync(string basePrompt, MutationContext context);
}
