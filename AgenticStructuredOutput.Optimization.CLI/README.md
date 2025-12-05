# AgenticStructuredOutput.Optimization.CLI

Command-line tool for running prompt optimization experiments. Reads and writes to solution-level resources, allowing source control to track prompt improvements.

## Overview

This CLI tool demonstrates the auto-prompt-optimization framework in action. It:
1. Loads the baseline agent prompt from solution root
2. Loads schema and test cases
3. Evaluates prompt against test cases
4. Applies mutation strategies to generate improved variants
5. Iteratively optimizes using hill-climbing algorithm
6. **Saves optimized prompt back to solution root** (source control tracks changes)

## Key Design

- **Solution-Level Resources**: Reads `agent-instructions.md` and `schema.json` from solution root
- **No Hardcoded Schema**: General-purpose optimizer, schema provided at runtime
- **Source Control Integration**: Overwrites solution files, letting git handle approval/rejection
- **No Code Duplication**: Uses shared `ResourceLoader` utility

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

### Running with Defaults

Loads resources from solution root:
```bash
cd AgenticStructuredOutput.Optimization.CLI
dotnet run
```

This will:
- Load `../../agent-instructions.md` (solution root)
- Load `../../schema.json` (solution root)
- Use hardcoded demo test cases
- Run 5 iterations of optimization
- **Save improved prompt back to `../../agent-instructions.md`**

### Running with Custom Paths

```bash
dotnet run -- \
  --schema /path/to/custom-schema.json \
  --prompt /path/to/custom-prompt.md \
  --test-cases /path/to/test-cases.jsonl \
  --max-iterations 10
```

### Command-Line Arguments

- `--schema <path>`: Path to schema JSON file (default: solution root/schema.json)
- `--prompt <path>`: Path to prompt file (default: solution root/agent-instructions.md)
- `--test-cases <path>`: Path to JSONL test cases file (default: demo cases)
- `--max-iterations <n>`: Maximum optimization iterations (default: 5)
- `--verbose`: Show detailed error stack traces

## Output

The tool will:
1. Display configuration and loaded resources
2. Show baseline metrics
3. Display progress through iterations
4. Report final optimization results
5. **Overwrite the prompt file with optimized version**

### Example Output

```
╔═══════════════════════════════════════════════════════════╗
║   AgenticStructuredOutput - Prompt Optimization CLI      ║
╚═══════════════════════════════════════════════════════════╝

Configuration:
  Schema: (solution root)/schema.json
  Prompt: (solution root)/agent-instructions.md
  Test Cases: (hardcoded demo cases)
  Max Iterations: 5

Loading resources from solution root...
Baseline Prompt Length: 1234 characters
Loaded 3 test cases

Starting optimization...
Enabled Strategies: AddExamples, AddConstraints

[... optimization progress ...]

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

✓ Optimized prompt saved to: /path/to/solution/agent-instructions.md
  (Source control will track this change)
```

## Source Control Workflow

After optimization:

1. **Review changes**: `git diff agent-instructions.md`
2. **Accept**: `git add agent-instructions.md && git commit -m "Optimize agent prompt"`
3. **Reject**: `git checkout agent-instructions.md`

This workflow ensures human approval of AI-generated prompt changes.

## Test Case Format

Load test cases from JSONL file:

```jsonl
{"id": "test-001", "evaluationType": "Relevance", "testScenario": "Simple mapping", "input": "{...}", "expectedOutput": "{...}"}
{"id": "test-002", "evaluationType": "Correctness", "testScenario": "Nested structure", "input": "{...}"}
```

Schema is loaded separately and applied to all test cases that don't specify their own schema.

## Architecture

- **ResourceLoader**: Shared utility for loading/saving solution-level resources
- **No Schema Dependency**: EvaluationAggregator doesn't require schema in constructor
- **Test Case Schema**: Each test case has its own schema (or inherits from loaded default)
- **General Purpose**: Works with any schema/prompt combination

## Performance

### Typical Run
- **3 test cases**: ~2-3 minutes for 5 iterations
- **10 test cases**: ~5-7 minutes for 10 iterations
- **API Cost**: ~$0.10-$0.30 per run (using gpt-4o-mini)

### Optimization Tips
- Use fewer test cases for faster iteration during development
- Enable parallel evaluation for better performance
- Reduce `--max-iterations` for quick experiments
- Use cheaper judge models if cost is a concern

## Troubleshooting

### "No API key found"
Set `GITHUB_TOKEN` or `OPENAI_API_KEY` environment variable.

### "Could not find solution root"
Ensure you're running from within the repository. The tool walks up directories looking for .slnx or .sln files.

### "Schema file not found"
Ensure `schema.json` exists in the solution root, or provide `--schema` argument.

### "Test case must have a schema defined"
Each test case needs a schema. Either:
- Let test cases inherit from loaded schema (default behavior)
- Provide schema in each test case JSON

## License

See LICENSE file for details.
