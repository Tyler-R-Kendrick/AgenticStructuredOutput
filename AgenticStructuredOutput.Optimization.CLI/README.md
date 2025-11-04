# AgenticStructuredOutput.Optimization.CLI

Command-line tool for running prompt optimization experiments.

## Overview

This CLI tool demonstrates the auto-prompt-optimization framework in action. It:
1. Loads the baseline agent prompt
2. Evaluates it against test cases
3. Applies mutation strategies to generate improved variants
4. Iteratively optimizes using hill-climbing algorithm
5. Reports results and saves the optimized prompt

## Usage

### Prerequisites

Set environment variable for GitHub Models authentication:
```bash
export GITHUB_TOKEN="your-github-token"
```

Or use OpenAI:
```bash
export OPENAI_API_KEY="your-openai-key"
```

### Running

```bash
cd AgenticStructuredOutput.Optimization.CLI
dotnet run
```

### Output

The tool will:
1. Display baseline metrics
2. Show progress through iterations
3. Report final optimization results
4. Save optimized prompt to `optimized-prompt.md`

### Example Output

```
╔═══════════════════════════════════════════════════════════╗
║   AgenticStructuredOutput - Prompt Optimization CLI      ║
╚═══════════════════════════════════════════════════════════╝

Baseline Prompt Length: 1234 characters

Loaded 3 test cases

Starting optimization...
Max Iterations: 5
Enabled Strategies: AddExamples, AddConstraints

info: AgenticStructuredOutput.Optimization.Evaluation.EvaluationAggregator[0]
      Evaluating prompt across 3 test cases
info: AgenticStructuredOutput.Optimization.Core.IterativeOptimizer[0]
      Iteration 1/5
info: AgenticStructuredOutput.Optimization.Core.IterativeOptimizer[0]
        Strategy 'AddExamples': Composite=3.85 (Δ=+0.23)
info: AgenticStructuredOutput.Optimization.Core.IterativeOptimizer[0]
        Strategy 'AddConstraints': Composite=3.92 (Δ=+0.30)
info: AgenticStructuredOutput.Optimization.Core.IterativeOptimizer[0]
      ✓ Accepted: Improvement of +0.30

...

═══════════════════════════════════════════════════════════
OPTIMIZATION RESULTS
═══════════════════════════════════════════════════════════

Baseline Score:     3.62
Best Score:         4.15
Improvement:        +0.53
Iterations:         5
Duration:           125.3s
Stopping Reason:    Max iterations reached

Metric Breakdown:
  Completeness    4.20 (Δ +0.45)
  Correctness     4.35 (Δ +0.52)
  Grounding       4.10 (Δ +0.60)
  Relevance       3.95 (Δ +0.55)

Iteration History:
   1. ✓ AddConstraints       Score: 3.92 (Δ +0.30)
   2. ✓ AddExamples          Score: 4.05 (Δ +0.13)
   3. ✗ RephraseInstructions Score: 4.02 (Δ -0.03)
   4. ✓ AddConstraints       Score: 4.12 (Δ +0.07)
   5. ✓ SimplifyLanguage     Score: 4.15 (Δ +0.03)

Optimized prompt saved to: /path/to/optimized-prompt.md
```

## Configuration

Modify `Program.cs` to adjust optimization settings:

```csharp
var config = new OptimizationConfig
{
    MaxIterations = 10,              // Number of optimization iterations
    ImprovementThreshold = 0.05,     // Minimum improvement required
    EnabledStrategies = new List<string>
    {
        "AddExamples",               // Add few-shot examples
        "AddConstraints",            // Add explicit rules
        "RephraseInstructions",      // Rephrase for clarity
        "SimplifyLanguage"           // Reduce verbosity
    },
    MetricWeights = new Dictionary<string, double>
    {
        { "Relevance", 1.0 },
        { "Correctness", 1.5 },      // Emphasize correctness
        { "Completeness", 1.0 },
        { "Grounding", 1.25 }
    },
    ParallelEvaluation = true,
    MaxParallelTasks = 4
};
```

## Test Cases

The demo uses a small set of hardcoded test cases. For production:

1. Load test cases from JSONL file:
```csharp
var testCases = File.ReadAllLines("test-cases-eval.jsonl")
    .Select(line => JsonSerializer.Deserialize<EvalTestCase>(line))
    .ToList();
```

2. Use the test cases from the Tests project:
```csharp
var testCasesPath = Path.Combine(
    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
    "..",
    "..",
    "AgenticStructuredOutput.Tests",
    "Resources",
    "test-cases-eval.jsonl"
);
```

## Customization

### Custom Mutation Strategies

Add your own strategies:

```csharp
services.AddSingleton<IEnumerable<IPromptMutationStrategy>>(sp =>
{
    return new List<IPromptMutationStrategy>
    {
        new AddExamplesStrategy(),
        new AddConstraintsStrategy(),
        new RephraseInstructionsStrategy(),
        new SimplifyLanguageStrategy(),
        new MyCustomStrategy()  // Add your own!
    };
});
```

### Custom Evaluation

Use different evaluators or test cases:

```csharp
services.AddSingleton<IEvaluationAggregator>(sp =>
{
    // Use custom evaluators, judge model, or test cases
    return new EvaluationAggregator(...);
});
```

## Performance

### Typical Run
- **3 test cases**: ~2-3 minutes for 5 iterations
- **10 test cases**: ~5-7 minutes for 10 iterations
- **API Cost**: ~$0.10-$0.30 per run (using gpt-4o-mini)

### Optimization Tips
- Use fewer test cases for faster iteration during development
- Enable parallel evaluation for better performance
- Reduce `MaxIterations` for quick experiments
- Use cheaper judge models if cost is a concern

## Troubleshooting

### "No API key found"
Set `GITHUB_TOKEN` or `OPENAI_API_KEY` environment variable.

### "GitHub Models not accessible"
Check that your GitHub token has appropriate permissions.

### Slow execution
- Reduce test case count
- Reduce `MaxIterations`
- Increase `MaxParallelTasks`

### Poor optimization results
- Add more diverse test cases
- Enable more mutation strategies
- Adjust metric weights
- Increase `MaxIterations`

## Future Enhancements

- Load test cases from file (JSONL, JSON, CSV)
- Export results to file (JSON, Markdown, HTML)
- Compare multiple optimization runs
- Visualize optimization progress (charts)
- Interactive mode (choose strategies per iteration)
- Batch optimization (multiple prompts at once)

## License

See LICENSE file for details.
