using AgenticStructuredOutput.Optimization.Core;
using AgenticStructuredOutput.Optimization.Models;
using System.Text;

namespace AgenticStructuredOutput.Optimization.Strategies;

/// <summary>
/// Adds few-shot examples to the prompt to improve accuracy.
/// </summary>
public class AddExamplesStrategy(int maxExamples = 3) : IPromptMutationStrategy
{
    public string Name => "AddExamples";
    public string Description => "Add few-shot examples to demonstrate desired input-output mappings";

    private readonly int _maxExamples = maxExamples;

    public Task<string> MutateAsync(string basePrompt, MutationContext context)
    {
        if (context.TestCases.Count == 0)
        {
            return Task.FromResult(basePrompt);
        }

        // Select diverse examples
        var examples = SelectDiverseExamples(context.TestCases, _maxExamples, context.Random);

        // Build examples section
        var examplesSection = new StringBuilder();
        examplesSection.AppendLine();
        examplesSection.AppendLine("## Examples");
        examplesSection.AppendLine();
        examplesSection.AppendLine("Here are some example mappings:");
        examplesSection.AppendLine();

        foreach (var example in examples)
        {
            examplesSection.AppendLine($"**Example {examples.IndexOf(example) + 1}:**");
            examplesSection.AppendLine("```json");
            examplesSection.AppendLine($"Input: {example.Input}");
            if (!string.IsNullOrEmpty(example.ExpectedOutput))
            {
                examplesSection.AppendLine($"Output: {example.ExpectedOutput}");
            }
            examplesSection.AppendLine("```");
            examplesSection.AppendLine();
        }

        // Append examples to the prompt
        var mutatedPrompt = basePrompt + "\n" + examplesSection.ToString();
        return Task.FromResult(mutatedPrompt);
    }

    private static List<EvalTestCase> SelectDiverseExamples(
        List<EvalTestCase> testCases,
        int maxExamples,
        Random random)
    {
        // For simplicity, randomly select examples
        // In a more sophisticated version, we'd select based on diversity
        var shuffled = testCases.OrderBy(_ => random.Next()).ToList();
        return shuffled.Take(Math.Min(maxExamples, testCases.Count)).ToList();
    }
}
