# AgenticStructuredOutput.Simulation

Generates evaluation test cases from prompts and schemas using LLM simulation. Creates JSONL files that can be used for prompt optimization.

## Overview

This library enables automatic generation of evaluation test cases when you don't have pre-existing evals. It:
1. Takes a prompt and schema as input
2. Uses an LLM to generate diverse, realistic test case inputs
3. Persists generated test cases to JSONL format
4. Integrates optionally with the Optimization project

## Use Cases

- **Bootstrap Evaluation**: Generate initial test cases when starting prompt development
- **Augment Existing Evals**: Expand test coverage with generated cases
- **Exploration**: Discover edge cases and scenarios you might not have considered
- **Iteration**: Quickly create test cases for each prompt variation

## Core Components

### Interfaces
- **`IEvalGenerator`**: Generates evaluation test cases from prompt + schema
- **`IEvalPersistence`**: Saves/loads test cases to/from JSONL files

### Implementations
- **`LlmEvalGenerator`**: Uses LLM to generate diverse test cases
- **`JsonLEvalPersistence`**: Persists to JSONL format

### Models
- **`SimulationConfig`**: Configuration for test case generation
- **`SimulationResult`**: Results from a simulation run

## Usage

### Basic Example

```csharp
using AgenticStructuredOutput.Simulation;
using AgenticStructuredOutput.Simulation.Core;
using AgenticStructuredOutput.Simulation.Models;

// Setup
var llmClient = CreateChatClient(); // Your IChatClient
var generator = new LlmEvalGenerator(llmClient, logger);
var persistence = new JsonLEvalPersistence(logger);

// Load prompt and schema
var prompt = ResourceLoader.LoadPrompt();
var schema = ResourceLoader.LoadSchema();

// Configure generation
var config = new SimulationConfig
{
    TestCaseCount = 20,
    EvaluationTypes = new List<string> { "Relevance", "Correctness", "Completeness", "Grounding" },
    IncludeEdgeCases = true,
    DiversityFactor = 0.8,
    Temperature = 0.9f
};

// Generate test cases
var result = await generator.GenerateTestCasesAsync(prompt, schema, config);

// Save to JSONL
await persistence.SaveTestCasesAsync(
    result.TestCases,
    "generated-test-cases.jsonl");
```

### Integration with Optimization

```csharp
// 1. Generate test cases if none exist
if (!File.Exists("test-cases-eval.jsonl"))
{
    var simConfig = new SimulationConfig { TestCaseCount = 15 };
    var simResult = await generator.GenerateTestCasesAsync(prompt, schema, simConfig);
    await persistence.SaveTestCasesAsync(simResult.TestCases, "test-cases-eval.jsonl");
}

// 2. Load generated test cases
var testCases = ResourceLoader.LoadTestCases("test-cases-eval.jsonl", schema);

// 3. Run optimization
var optConfig = new OptimizationConfig { MaxIterations = 10 };
var optResult = await optimizer.OptimizeAsync(prompt, testCases, optConfig);
```

## Configuration Options

### SimulationConfig

```csharp
var config = new SimulationConfig
{
    // Number of test cases to generate
    TestCaseCount = 10,
    
    // Types of evaluations (affects generation prompts)
    EvaluationTypes = new List<string> 
    { 
        "Relevance",      // Focus on semantic matching
        "Correctness",    // Focus on accuracy
        "Completeness",   // Focus on coverage
        "Grounding"       // Focus on data fidelity
    },
    
    // Generate expected outputs (requires more LLM calls)
    GenerateExpectedOutputs = false,
    
    // Diversity factor (0.0 = conservative, 1.0 = highly creative)
    DiversityFactor = 0.7,
    
    // LLM temperature for generation
    Temperature = 0.8f,
    
    // Include edge cases and unusual scenarios
    IncludeEdgeCases = true
};
```

## Generated Test Case Format

Output JSONL format:
```jsonl
{"evaluationType": "Relevance", "testScenario": "Simple name mapping", "input": "{\"firstName\": \"John\", ...}"}
{"evaluationType": "Correctness", "testScenario": "Nested structure", "input": "{\"person\": {...}}"}
```

Each line is a JSON object with:
- `evaluationType`: Category of evaluation
- `testScenario`: Brief description
- `input`: JSON input data as string
- `schema`: (added during loading) JSON schema element

## Best Practices

### 1. Start Small
Generate a small batch first (5-10 cases) to verify quality:
```csharp
var config = new SimulationConfig { TestCaseCount = 5 };
```

### 2. Review Generated Cases
Inspect the JSONL file before using for optimization:
```bash
cat generated-test-cases.jsonl | jq .
```

### 3. Mix Generated and Manual
Combine generated cases with manually crafted ones:
```csharp
var generated = await persistence.LoadTestCasesAsync("generated.jsonl");
var manual = await persistence.LoadTestCasesAsync("manual.jsonl");
var all = generated.Concat(manual).ToList();
```

### 4. Iterate on Configuration
Adjust `DiversityFactor` and `Temperature` based on results:
- Lower values → more conservative, predictable cases
- Higher values → more creative, diverse scenarios

### 5. Use for Exploration
Generate many cases to discover edge cases:
```csharp
var config = new SimulationConfig 
{ 
    TestCaseCount = 50,
    IncludeEdgeCases = true,
    DiversityFactor = 0.9
};
```

## Performance

### Typical Generation
- **5 test cases**: ~10-15 seconds
- **20 test cases**: ~30-40 seconds
- **50 test cases**: ~90-120 seconds

### Cost (using gpt-4o-mini)
- ~$0.01-0.02 per batch of 5 test cases
- ~$0.10-0.15 for 50 test cases

### Optimization
- Batches of 5 cases processed in parallel
- Configurable temperature for cost/quality trade-off
- Caching of generated cases

## Workflow Examples

### Scenario 1: Bootstrap New Prompt
```csharp
// 1. Create initial prompt and schema
var prompt = "Map input JSON to standard person schema";
var schema = LoadSchema("person-schema.json");

// 2. Generate test cases
var config = new SimulationConfig { TestCaseCount = 10 };
var result = await generator.GenerateTestCasesAsync(prompt, schema, config);

// 3. Save for future use
await persistence.SaveTestCasesAsync(result.TestCases, "test-cases.jsonl");

// 4. Optimize using generated cases
var optResult = await optimizer.OptimizeAsync(prompt, result.TestCases, optConfig);
```

### Scenario 2: Augment Existing Evals
```csharp
// 1. Load existing test cases
var existing = await persistence.LoadTestCasesAsync("existing-cases.jsonl");

// 2. Generate additional cases
var config = new SimulationConfig 
{ 
    TestCaseCount = 10,
    DiversityFactor = 0.9  // High diversity to avoid duplicates
};
var result = await generator.GenerateTestCasesAsync(prompt, schema, config);

// 3. Combine and save
var combined = existing.Concat(result.TestCases).ToList();
await persistence.SaveTestCasesAsync(combined, "combined-cases.jsonl");
```

### Scenario 3: Iterative Refinement
```csharp
// 1. Generate initial test cases
var simResult = await generator.GenerateTestCasesAsync(prompt, schema, simConfig);

// 2. Optimize prompt
var optResult = await optimizer.OptimizeAsync(prompt, simResult.TestCases, optConfig);

// 3. Generate new test cases for improved prompt
var newSimResult = await generator.GenerateTestCasesAsync(
    optResult.BestPrompt, 
    schema, 
    simConfig);

// 4. Optimize again with expanded test cases
var allCases = simResult.TestCases.Concat(newSimResult.TestCases).ToList();
var finalResult = await optimizer.OptimizeAsync(optResult.BestPrompt, allCases, optConfig);
```

## Limitations

1. **Quality Depends on LLM**: Generated cases are only as good as the LLM's understanding
2. **No Ground Truth**: Generated expected outputs may not be perfect
3. **Coverage Gaps**: May miss specific edge cases you care about
4. **Cost**: Generating many test cases requires LLM calls

## Recommendations

- **Review Generated Cases**: Always inspect before production use
- **Combine Approaches**: Mix generated and manually crafted test cases
- **Start Conservative**: Begin with low diversity, increase as needed
- **Validate Results**: Run generated cases through manual review
- **Iterate**: Generate → Review → Adjust config → Repeat

## Future Enhancements

- Template-based generation for consistent patterns
- Mutation of existing test cases
- Difficulty levels (easy/medium/hard)
- Schema-aware generation (respects field types, constraints)
- Adversarial case generation
- Test case clustering and deduplication

## License

See LICENSE file for details.
