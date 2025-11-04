using AgenticStructuredOutput.Optimization.Core;
using AgenticStructuredOutput.Optimization.Models;
using System.Text.RegularExpressions;

namespace AgenticStructuredOutput.Optimization.Strategies;

/// <summary>
/// Rephrases prompt instructions for clarity (using pattern-based transformation).
/// Note: For production, consider using LLM-based rephrasing for better quality.
/// </summary>
public partial class RephraseInstructionsStrategy : IPromptMutationStrategy
{
    public string Name => "RephraseInstructions";
    public string Description => "Rephrase instructions for better clarity and specificity";

    public Task<string> MutateAsync(string basePrompt, MutationContext context)
    {
        var rephrased = RephrasePrompt(basePrompt, context);
        return Task.FromResult(rephrased);
    }

    private static string RephrasePrompt(string prompt, MutationContext context)
    {
        // Focus on the weakest metric for targeted improvement
        var targetMetric = context.TargetImprovement ?? "general";

        switch (targetMetric.ToLowerInvariant())
        {
            case "relevance":
                return AddRelevanceFocus(prompt);
            case "correctness":
            case "equivalence":
                return AddCorrectnessFocus(prompt);
            case "completeness":
                return AddCompletenessFocus(prompt);
            case "grounding":
            case "groundedness":
                return AddGroundingFocus(prompt);
            default:
                return ImproveClarity(prompt);
        }
    }

    private static string AddRelevanceFocus(string prompt)
    {
        // Add emphasis on relevance
        var addition = @"

## Focus on Relevance
Ensure that every output field is directly relevant to the input data and schema requirements.
Map fields based on semantic meaning, not just naming similarity.";
        
        return prompt + addition;
    }

    private static string AddCorrectnessFocus(string prompt)
    {
        // Add emphasis on correctness
        var addition = @"

## Focus on Correctness
Double-check that:
- Field values match their input sources exactly
- Data types conform to schema requirements
- Field names map correctly (e.g., 'emailAddress' → 'email', 'yearsOld' → 'age')";
        
        return prompt + addition;
    }

    private static string AddCompletenessFocus(string prompt)
    {
        // Add emphasis on completeness
        var addition = @"

## Focus on Completeness
Ensure all required schema fields are populated from available input data.
Do not omit required fields - find the best match in the input.";
        
        return prompt + addition;
    }

    private static string AddGroundingFocus(string prompt)
    {
        // Add emphasis on grounding
        var addition = @"

## Focus on Data Grounding
CRITICAL: Only use data that actually exists in the input.
- Never fabricate or guess field values
- If uncertain about a mapping, omit the optional field
- Preserve exact values from input without modification";
        
        return prompt + addition;
    }

    private static string ImproveClarity(string prompt)
    {
        // Generic clarity improvements
        var improved = prompt;

        // Make instructions more specific
        improved = Regex.Replace(
            improved,
            @"intelligently map",
            "map each field by finding the semantically equivalent input field",
            RegexOptions.IgnoreCase);

        improved = Regex.Replace(
            improved,
            @"fuzzy matching",
            "semantic field matching (e.g., 'first_name' → 'firstName', 'emailAddress' → 'email')",
            RegexOptions.IgnoreCase);

        return improved;
    }
}
