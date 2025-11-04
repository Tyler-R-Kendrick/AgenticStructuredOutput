using AgenticStructuredOutput.Optimization.Core;
using AgenticStructuredOutput.Optimization.Models;
using System.Text;

namespace AgenticStructuredOutput.Optimization.Strategies;

/// <summary>
/// Adds explicit constraints to the prompt to prevent common failure modes.
/// </summary>
public class AddConstraintsStrategy : IPromptMutationStrategy
{
    public string Name => "AddConstraints";
    public string Description => "Add explicit rules and constraints based on evaluation weaknesses";

    private readonly string? _focusArea;

    public AddConstraintsStrategy(string? focusArea = null)
    {
        _focusArea = focusArea;
    }

    public Task<string> MutateAsync(string basePrompt, MutationContext context)
    {
        var constraints = GenerateConstraints(context);
        
        if (constraints.Count == 0)
        {
            return Task.FromResult(basePrompt);
        }

        var constraintsSection = new StringBuilder();
        constraintsSection.AppendLine();
        constraintsSection.AppendLine("## Critical Constraints");
        constraintsSection.AppendLine();
        
        for (int i = 0; i < constraints.Count; i++)
        {
            constraintsSection.AppendLine($"{i + 1}. {constraints[i]}");
        }
        constraintsSection.AppendLine();

        var mutatedPrompt = basePrompt + "\n" + constraintsSection.ToString();
        return Task.FromResult(mutatedPrompt);
    }

    private List<string> GenerateConstraints(MutationContext context)
    {
        var constraints = new List<string>();

        // Determine which constraints to add based on focus area or weaknesses
        var targetMetric = _focusArea ?? context.TargetImprovement ?? "General";

        switch (targetMetric.ToLowerInvariant())
        {
            case "grounding":
            case "groundedness":
                constraints.Add("NEVER invent or fabricate data not present in the input");
                constraints.Add("ALWAYS preserve exact values from the input without modification");
                constraints.Add("If a field value is uncertain, omit it rather than guessing");
                break;

            case "completeness":
                constraints.Add("ONLY include fields that exist in the input OR are required by the schema");
                constraints.Add("NEVER include optional fields with null/empty values if not in input");
                constraints.Add("Ensure all required schema fields are mapped from the input");
                break;

            case "correctness":
            case "equivalence":
                constraints.Add("Map field names using semantic understanding (e.g., 'emailAddress' → 'email')");
                constraints.Add("Maintain correct data types as specified in the schema");
                constraints.Add("Handle nested structures according to schema requirements");
                break;

            case "relevance":
                constraints.Add("Focus on mapping fields that are relevant to the schema");
                constraints.Add("Ignore input fields that have no corresponding schema field");
                constraints.Add("Maintain semantic relevance between input and output");
                break;

            default:
                // General constraints applicable to all cases
                constraints.Add("ONLY include fields that exist in the input OR are required by the schema");
                constraints.Add("NEVER include optional fields with null/empty values if not in input");
                constraints.Add("ALWAYS preserve exact values from the input without modification");
                constraints.Add("Use semantic field matching (e.g., 'first_name' → 'firstName')");
                constraints.Add("If a field cannot be mapped confidently, omit it rather than guessing");
                break;
        }

        return constraints;
    }
}
