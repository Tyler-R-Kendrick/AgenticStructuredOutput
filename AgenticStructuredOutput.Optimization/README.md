# AgenticStructuredOutput.Optimization

Auto-prompt-optimization library for improving agent prompts based on evaluation metrics.

## Overview

This project implements a .NET-native prompt optimization framework that automatically improves agent prompts by:
1. Evaluating prompt quality across multiple dimensions (Relevance, Correctness, Completeness, Grounding)
2. Applying mutation strategies to generate improved variants
3. Using hill-climbing optimization to iteratively refine prompts
4. Tracking optimization history and metrics

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Prompt Optimization Pipeline                           │
├─────────────────────────────────────────────────────────┤
│  1. Baseline Evaluation                                 │
│     └─ Aggregate metrics across test cases             │
│  2. Iterative Optimization Loop                         │
│     ├─ Select strategies based on weaknesses           │
│     ├─ Generate candidate prompts (mutations)          │
│     ├─ Evaluate each candidate                         │
│     ├─ Compare against current best                    │
│     └─ Accept if improved                              │
│  3. Return Best Prompt + Metrics                        │
└─────────────────────────────────────────────────────────┘
```

## Core Components

### Utilities
- **`ResourceLoader`**: Shared utility for loading/saving solution-level resources (schema, prompts)

### Models
- **`EvalTestCase`**: Test case for evaluation (includes schema per test case)
- **`AggregatedMetrics`**: Metrics aggregated across test cases
- **`OptimizationConfig`**: Configuration for optimization
- **`OptimizationResult`**: Results of optimization run
- **`PromptComparison`**: Comparison between two prompts

### Interfaces
- **`IPromptOptimizer`**: Main optimization interface
- **`IEvaluationAggregator`**: Evaluates and aggregates metrics (no hardcoded schema)
- **`IPromptMutationStrategy`**: Strategy for generating prompt variants

### Implementations

#### Resource Management
- **`ResourceLoader`**: Loads resources from solution root, walks up directory tree to find .slnx/.sln file

#### Mutation Strategies
1. **`AddExamplesStrategy`**: Adds few-shot examples to demonstrate desired mappings
2. **`AddConstraintsStrategy`**: Adds explicit rules to prevent failure modes
3. **`RephraseInstructionsStrategy`**: Rephrases for clarity and focus
4. **`SimplifyLanguageStrategy`**: Reduces verbosity while preserving meaning

#### Optimizer
- **`IterativeOptimizer`**: Hill-climbing algorithm with:
  - Adaptive strategy selection based on weaknesses
  - Random restarts to escape local minima
  - Lateral move acceptance for exploration
  - Early stopping for excellent scores

#### Evaluation
- **`EvaluationAggregator`**: Evaluates prompts using:
  - LLM-as-judge pattern (RelevanceEvaluator, EquivalenceEvaluator, etc.)
  - Parallel test case evaluation
  - Composite scoring with configurable weights

## Usage Example

```csharp
// Setup
var agentFactory = serviceProvider.GetRequiredService<IAgentFactory>();
var judgeClient = CreateJudgeClient();
var defaultSchema = LoadSchema();

var evaluator = new EvaluationAggregator(
    agentFactory,
    judgeClient,
    defaultSchema,
    logger);

var strategies = new List<IPromptMutationStrategy>
{
    new AddExamplesStrategy(),
    new AddConstraintsStrategy(),
    new RephraseInstructionsStrategy(),
    new SimplifyLanguageStrategy()
};

var optimizer = new IterativeOptimizer(evaluator, strategies, logger);

// Load test cases
var testCases = LoadTestCases("test-cases-eval.jsonl");

// Run optimization
var config = new OptimizationConfig
{
    MaxIterations = 10,
    ImprovementThreshold = 0.05,
    EnabledStrategies = new List<string> 
    { 
        "AddExamples", 
        "AddConstraints" 
    }
};

var result = await optimizer.OptimizeAsync(
    baselinePrompt,
    testCases,
    config,
    cancellationToken);

// Use optimized prompt
Console.WriteLine($"Best Prompt:\n{result.BestPrompt}");
Console.WriteLine($"Composite Score: {result.BestMetrics.CompositeScore:F2}");
Console.WriteLine($"Improvement: {result.TotalImprovement:+F2}");
```

## Configuration

### Optimization Config

```csharp
var config = new OptimizationConfig
{
    // Iteration limits
    MaxIterations = 10,
    
    // Acceptance thresholds
    ImprovementThreshold = 0.05,      // 5% improvement required
    LateralMoveThreshold = -0.02,     // Allow small regressions
    LateralMoveProbability = 0.1,     // 10% chance to accept lateral
    
    // Restart behavior
    RestartAfterStuckIterations = 3,  // Restart if stuck 3 iterations
    EarlyStoppingScore = 4.5,         // Stop if score >= 4.5
    
    // Strategies
    EnabledStrategies = new List<string>
    {
        "AddExamples",
        "AddConstraints",
        "RephraseInstructions",
        "SimplifyLanguage"
    },
    
    // Metric weights
    MetricWeights = new Dictionary<string, double>
    {
        { "Relevance", 1.0 },
        { "Correctness", 1.5 },      // Higher weight
        { "Completeness", 1.0 },
        { "Grounding", 1.25 }
    },
    
    // Performance
    ParallelEvaluation = true,
    MaxParallelTasks = 4
};
```

## Evaluation Metrics

### Primary Metrics (LLM-as-Judge)
- **Relevance** (1-5): Output relevance to input and schema
- **Correctness/Equivalence** (1-5): Semantic correctness of mapping
- **Completeness** (1-5): All required fields present
- **Grounding** (1-5): Output grounded in input data (no hallucination)

### Composite Score
Weighted average of metrics:
```
CompositeScore = Σ(weight_i × metric_i) / Σ(weights)
```

### Pass Criteria
- Individual test case passes if all metrics >= 3.0
- Prompt improvement requires composite score increase > threshold
- No major regressions (> 0.5) in individual metrics

## Mutation Strategies

### AddExamplesStrategy
**Goal**: Improve accuracy through few-shot learning

**Method**: Selects diverse examples from test cases and adds them to prompt

**Best for**: Improving Correctness, Relevance

### AddConstraintsStrategy
**Goal**: Prevent common failure modes with explicit rules

**Method**: Adds constraints based on target weakness (e.g., "NEVER invent data" for Grounding)

**Best for**: Improving Grounding, Completeness

### RephraseInstructionsStrategy
**Goal**: Improve clarity and focus

**Method**: Adds metric-specific guidance and clarifications

**Best for**: Improving Relevance

### SimplifyLanguageStrategy
**Goal**: Reduce verbosity, improve focus

**Method**: Removes redundant phrases, simplifies complex sentences

**Best for**: Reducing token usage, improving clarity

## Optimization Algorithm

**Hill Climbing with Random Restarts**:
1. Start with baseline prompt
2. Generate candidate variants using strategies
3. Evaluate each candidate
4. Select best candidate
5. Accept if improved or lateral move (probabilistic)
6. Random restart if stuck for N iterations
7. Early stop if excellent score
8. Repeat until max iterations

**Adaptive Strategy Selection**:
- Identifies weakest metric
- Selects strategy targeting that weakness
- Falls back to exploration strategies

## Performance Characteristics

### Typical Run
- **Iterations to convergence**: 5-7
- **API calls per iteration**: ~40 (4 strategies × 10 test cases)
- **Total API calls**: ~200-280
- **Time per iteration**: ~20-30 seconds
- **Total time**: ~2-4 minutes
- **Cost per run**: ~$0.20-$0.40 (using gpt-4o-mini)

### Optimization
- Parallel evaluation of test cases
- Configurable parallelism (default: 4 concurrent tasks)
- Early stopping for excellent scores
- Adaptive strategy selection reduces wasted evaluations

## Dependencies

- **Microsoft.Extensions.AI** (9.10.1): AI abstraction layer
- **Microsoft.Extensions.AI.Evaluation** (9.10.0): Evaluation framework
- **Microsoft.Extensions.AI.Evaluation.Quality** (9.10.0): Quality evaluators
- **AgenticStructuredOutput**: Main project (agent factory, services)

## Design Decisions

See ADRs in `/docs/adr/`:
- **ADR-001**: Prompt Optimization Strategy Selection (.NET-native approach)
- **ADR-002**: Evaluation Framework Design
- **ADR-003**: Prompt Mutation Strategies  
- **ADR-004**: Optimization Algorithm (Hill Climbing)
- **ADR-005**: .NET vs Python Trade-offs

## Future Enhancements

1. **LLM-based Rephrasing**: Use LLM to rephrase instructions (currently pattern-based)
2. **Meta-Learning**: Learn which strategies work best for which weaknesses
3. **Multi-Schema Optimization**: Optimize across multiple schema types
4. **A/B Testing Integration**: Production testing of prompt variants
5. **Automated Test Case Generation**: LLM generates diverse test cases
6. **Prompt Versioning**: Git-like versioning for prompts
7. **Transfer Learning**: Reuse optimization results across similar schemas

## Contributing

When adding new mutation strategies:
1. Implement `IPromptMutationStrategy`
2. Provide clear `Name` and `Description`
3. Make mutation deterministic or accept `MutationContext.Random` for reproducibility
4. Add strategy to optimizer's enabled list
5. Write unit tests

## License

See LICENSE file for details.
