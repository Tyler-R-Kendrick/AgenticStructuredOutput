# ADR-002: Evaluation Framework Design for Prompt Optimization

**Status**: Proposed  
**Date**: 2025-11-04  
**Deciders**: Development Team  
**Context Tags**: Architecture, Testing, Evaluation

## Context

To optimize prompts automatically, we need a robust evaluation framework that can:
1. Measure prompt quality across multiple dimensions
2. Aggregate scores across test cases
3. Compare prompt variants objectively
4. Provide actionable feedback for optimization

The project already uses `Microsoft.Extensions.AI.Evaluation` with LLM judges, but we need to design how the optimization system will interact with and extend this framework.

## Current Evaluation State

**Existing Implementation** (`AgentEvaluationTests.cs`):
- Uses `Microsoft.Extensions.AI.Evaluation.Quality` evaluators
- Metrics: Relevance, Correctness (Equivalence), Completeness, Grounding
- LLM-as-Judge pattern with `gpt-4o-mini` as judge model
- Test cases in JSONL format (`test-cases-eval.jsonl`)
- Numeric scores (1-5 scale)

**Gap**: No aggregation, comparison, or optimization loop infrastructure.

## Decision

Design evaluation framework with three layers:

### Layer 1: Metric Collection (Existing)
Use existing `IEvaluator` implementations from `Microsoft.Extensions.AI.Evaluation`.

### Layer 2: Aggregation & Comparison (NEW)
```csharp
public interface IEvaluationAggregator
{
    Task<AggregatedMetrics> EvaluatePromptAsync(
        string prompt, 
        IEnumerable<EvalTestCase> testCases);
        
    Task<PromptComparison> ComparePromptsAsync(
        AggregatedMetrics baseline,
        AggregatedMetrics candidate);
}

public class AggregatedMetrics
{
    public string PromptId { get; set; }
    public Dictionary<string, double> AverageScores { get; set; }
    public Dictionary<string, double> MinScores { get; set; }
    public Dictionary<string, double> MaxScores { get; set; }
    public Dictionary<string, double> StdDeviation { get; set; }
    public int TestCaseCount { get; set; }
    public double CompositeScore { get; set; }  // Weighted average
}

public class PromptComparison
{
    public bool IsImprovement { get; set; }
    public double DeltaCompositeScore { get; set; }
    public Dictionary<string, double> DeltaByMetric { get; set; }
    public string Summary { get; set; }
}
```

### Layer 3: Optimization Loop (NEW)
```csharp
public interface IPromptOptimizer
{
    Task<OptimizationResult> OptimizeAsync(
        string baselinePrompt,
        IEnumerable<EvalTestCase> testCases,
        OptimizationConfig config);
}

public class OptimizationConfig
{
    public int MaxIterations { get; set; } = 10;
    public double ImprovementThreshold { get; set; } = 0.05;
    public List<string> EnabledStrategies { get; set; }
    public Dictionary<string, double> MetricWeights { get; set; }
}

public class OptimizationResult
{
    public string BestPrompt { get; set; }
    public AggregatedMetrics BestMetrics { get; set; }
    public List<OptimizationIteration> History { get; set; }
    public TimeSpan Duration { get; set; }
}
```

## Evaluation Metrics

### Primary Metrics (Equal Weight by Default)
1. **Relevance** (1-5): Output relevance to input
2. **Correctness/Equivalence** (1-5): Semantic correctness
3. **Completeness** (1-5): All required fields present
4. **Grounding** (1-5): Output grounded in input data

### Composite Score Calculation
```csharp
CompositeScore = Σ(weight_i × metric_i) / Σ(weights)
```

Default weights (can be overridden):
```json
{
  "Relevance": 1.0,
  "Correctness": 1.5,  // Slightly higher weight for correctness
  "Completeness": 1.0,
  "Grounding": 1.25
}
```

### Success Criteria
A prompt variant is considered improved if:
- `candidate.CompositeScore > baseline.CompositeScore + threshold` (default: +0.05)
- No individual metric regresses by more than 0.5 points
- At least 80% of test cases pass (score ≥ 3.0)

## Test Case Management

### Test Case Structure
```json
{
  "id": "test-001",
  "evaluationType": "Relevance",
  "testScenario": "Fuzzy name mapping",
  "input": "{\"first_name\": \"John\", \"last_name\": \"Doe\"}",
  "expectedOutput": "{\"firstName\": \"John\", \"lastName\": \"Doe\"}",  // Optional
  "schema": "{...}"  // Optional override
}
```

### Test Case Categories
1. **Basic Mapping**: Simple field renames
2. **Fuzzy Matching**: Semantic field mapping
3. **Nested Structures**: Object hierarchy mapping
4. **Type Inference**: String to integer, etc.
5. **Edge Cases**: Missing fields, empty values

### Test Case Sources
- `test-cases-eval.jsonl` (existing)
- `test-cases-integration.jsonl` (existing)
- Dynamically generated cases (future)

## Evaluation Execution Flow

```
┌─────────────────────────────────────────────────────────┐
│  1. Load Test Cases from JSONL                          │
└──────────────────┬──────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────┐
│  2. For Each Prompt Variant                             │
│     ├─ For Each Test Case                               │
│     │   ├─ Invoke Agent with Prompt                     │
│     │   ├─ Collect Response                             │
│     │   └─ Evaluate with LLM Judges                     │
│     │       ├─ RelevanceEvaluator                       │
│     │       ├─ EquivalenceEvaluator                     │
│     │       ├─ CompletenessEvaluator                    │
│     │       └─ GroundednessEvaluator                    │
│     └─ Aggregate Metrics                                │
└──────────────────┬──────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────┐
│  3. Compare Against Baseline                            │
│     ├─ Calculate Δ Composite Score                      │
│     ├─ Check Individual Metric Regressions              │
│     └─ Determine if Improvement                         │
└──────────────────┬──────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────┐
│  4. Update Best Prompt if Improved                      │
└─────────────────────────────────────────────────────────┘
```

## Performance Considerations

### API Call Optimization
- **Problem**: Each evaluation requires multiple LLM calls (1 for agent + 4 for judges)
- **Solution 1**: Batch test case evaluation (parallelization)
- **Solution 2**: Cache judge responses for identical agent outputs
- **Solution 3**: Use cheaper judge model (gpt-4o-mini vs gpt-4)

### Estimated Costs (per iteration)
- 10 test cases × 5 LLM calls = 50 API calls
- At $0.15/$0.60 per 1M tokens (gpt-4o-mini input/output)
- ~500 tokens input + ~200 tokens output per call
- **Cost per iteration**: ~$0.005-$0.01
- **10 iterations**: ~$0.05-$0.10

### Time Estimates
- Single test case evaluation: ~2-5 seconds
- 10 test cases in parallel: ~5-10 seconds
- Full optimization (10 iterations): ~1-2 minutes

## Alternative Approaches Considered

### Alt 1: Rule-Based Evaluation (Rejected)
**Approach**: Define deterministic rules instead of LLM judges.

**Pros**: Fast, deterministic, no API costs

**Cons**: 
- Cannot handle semantic variations
- Brittle to schema changes
- Misses nuanced quality issues
- Current LLM judges already integrated

**Verdict**: Rejected - LLM judges provide better quality signals

### Alt 2: Human-in-the-Loop (Deferred)
**Approach**: Human reviews a sample of outputs for each prompt variant.

**Pros**: Highest quality feedback

**Cons**: 
- Not automated
- Slow (hours vs minutes)
- Expensive (human time)
- Not scalable

**Verdict**: Deferred - consider for final validation only

### Alt 3: Outcome-Based Metrics (Future)
**Approach**: Measure downstream task success (e.g., user satisfaction).

**Pros**: Aligned with real-world value

**Cons**: 
- Requires production deployment
- Slow feedback loop
- Hard to attribute to prompt changes

**Verdict**: Future consideration - not for initial implementation

## Implementation Risks

### Risk 1: LLM Judge Inconsistency
**Mitigation**: 
- Use temperature=0 for deterministic scoring
- Average scores across multiple test cases
- Track variance in judge scores

### Risk 2: Overfitting to Test Cases
**Mitigation**:
- Maintain holdout test set
- Regularly refresh test cases
- Monitor performance on new schemas

### Risk 3: Local Minima in Optimization
**Mitigation**:
- Use multiple mutation strategies
- Implement random restarts
- Allow lateral moves occasionally

## Consequences

### Positive
- Objective, quantitative prompt comparison
- Automated optimization feedback loop
- Reuses existing evaluation infrastructure
- Scalable to more test cases

### Negative
- API costs for LLM judges (though minimal)
- Potential judge inconsistency
- Optimization time (minutes per run)

### Mitigation
- Cache evaluations where possible
- Use efficient parallelization
- Monitor and report judge variance
- Provide fast-feedback mode (subset of test cases)

## Future Enhancements

1. **Multi-Schema Optimization**: Optimize prompts across multiple schema types
2. **A/B Testing Framework**: Production A/B testing of prompt variants
3. **Automated Test Case Generation**: LLM generates diverse test cases
4. **Fine-tuned Judge Models**: Train smaller, faster judge models
5. **Prompt Versioning Service**: Track prompt history with Git-like semantics

## References

- [Microsoft.Extensions.AI.Evaluation Docs](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.evaluation)
- [LLM-as-Judge Pattern](https://arxiv.org/abs/2306.05685)
- [Prometheus: LLM Evaluation Framework](https://arxiv.org/abs/2310.08491)

## Acceptance Criteria

- [ ] `IEvaluationAggregator` interface defined
- [ ] Composite score calculation implemented
- [ ] Prompt comparison logic implemented
- [ ] Support for custom metric weights
- [ ] Parallel test case evaluation
- [ ] Comprehensive unit tests for aggregation logic
- [ ] Integration tests with existing evaluators
- [ ] Documentation with examples
