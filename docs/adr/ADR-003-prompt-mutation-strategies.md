# ADR-003: Prompt Mutation Strategies

**Status**: Proposed  
**Date**: 2025-11-04  
**Deciders**: Development Team  
**Context Tags**: AI/ML, Optimization, Algorithms

## Context

To optimize prompts automatically, we need strategies for generating improved prompt variants. These strategies must be:
1. **Systematic**: Generate variants in a principled way
2. **Diverse**: Explore different aspects of prompt design
3. **Focused**: Target specific quality dimensions
4. **Practical**: Work within LLM token limits and cognitive constraints

## Decision

Implement **four core mutation strategies** with a plugin architecture for future extensibility.

## Core Mutation Strategies

### Strategy 1: Add Examples (Few-Shot Learning)

**Purpose**: Improve accuracy by providing concrete examples of desired input→output mappings.

**Implementation**:
```csharp
public class AddExamplesStrategy : IPromptMutationStrategy
{
    public string Mutate(string basePrompt, MutationContext context)
    {
        var examples = GenerateExamplesFromTestCases(context.TestCases);
        return $"{basePrompt}\n\n## Examples\n\n{examples}";
    }
}
```

**Example Mutation**:
```markdown
Original:
---
Map the following JSON input to the target schema using fuzzy logic.

Mutated:
---
Map the following JSON input to the target schema using fuzzy logic.

## Examples

Input: {"first_name": "John", "last_name": "Doe"}
Output: {"firstName": "John", "lastName": "Doe"}

Input: {"person": {"givenName": "Jane", "familyName": "Smith"}}
Output: {"firstName": "Jane", "lastName": "Smith"}
```

**Parameters**:
- `maxExamples`: Number of examples to include (default: 3)
- `exampleSelectionStrategy`: Random, diverse, or representative
- `includeComplexExamples`: Include nested/edge cases

**Expected Improvements**:
- ✅ Correctness: +10-20% (clearer expectations)
- ✅ Grounding: +5-15% (examples show data preservation)
- ⚠️ Relevance: Minimal change
- ⚠️ Risk: May overfit to example patterns

---

### Strategy 2: Rephrase Instructions

**Purpose**: Find clearer/more effective phrasing using LLM to rewrite instructions.

**Implementation**:
```csharp
public class RephraseInstructionsStrategy : IPromptMutationStrategy
{
    private readonly IChatClient _llmClient;
    
    public async Task<string> MutateAsync(string basePrompt, MutationContext context)
    {
        var rephrasingPrompt = $@"
Rephrase the following prompt to be clearer and more effective.
Maintain the same intent but improve clarity and specificity.
Focus on: {context.TargetImprovement}

Original Prompt:
{basePrompt}

Rephrased Prompt:";
        
        var response = await _llmClient.CompleteAsync(rephrasingPrompt);
        return response.Text;
    }
}
```

**Example Mutation**:
```markdown
Original:
---
Use intelligent inference to map input fields to schema fields.
Apply fuzzy matching when field names don't exactly match.

Rephrased:
---
Intelligently map each input field to the corresponding schema field, even when 
field names differ. For example, map "emailAddress" to "email", "yearsOld" to 
"age", or "first_name" to "firstName". Use semantic understanding to find the 
best match for each field.
```

**Parameters**:
- `rephrasingModel`: Which LLM to use (default: gpt-4o-mini)
- `targetImprovement`: Specific weakness to address (e.g., "clarity", "specificity")
- `preserveSections`: Sections that must not change (e.g., schema references)

**Expected Improvements**:
- ✅ Relevance: +5-15% (clearer intent)
- ✅ Correctness: +5-10% (fewer ambiguities)
- ⚠️ Unpredictable: LLM rephrasing may introduce noise
- ⚠️ Risk: May lose important constraints

---

### Strategy 3: Add Constraints

**Purpose**: Add explicit rules to prevent common failure modes.

**Implementation**:
```csharp
public class AddConstraintsStrategy : IPromptMutationStrategy
{
    public string Mutate(string basePrompt, MutationContext context)
    {
        var constraints = DeriveConstraintsFromFailures(context.FailedTestCases);
        return $"{basePrompt}\n\n## Critical Constraints\n\n{constraints}";
    }
    
    private string DeriveConstraintsFromFailures(List<TestCaseResult> failures)
    {
        // Analyze failure patterns and generate constraints
        // E.g., if completeness failed: "ALWAYS include all required fields"
        // E.g., if grounding failed: "NEVER invent data not present in input"
    }
}
```

**Example Mutation**:
```markdown
Original:
---
Map the following JSON input to the target schema.

Mutated:
---
Map the following JSON input to the target schema.

## Critical Constraints

1. ONLY include fields that exist in the input OR are required by the schema
2. NEVER include optional fields with null/empty values if not in input
3. ALWAYS preserve exact values from input (no transformation)
4. For nested objects, flatten ONLY when schema requires it
5. If a field cannot be mapped, omit it (don't guess)
```

**Parameters**:
- `maxConstraints`: Maximum constraints to add (default: 5)
- `constraintSource`: Derived from failures or predefined
- `constraintFormat`: Numbered list, bullet points, or paragraphs

**Expected Improvements**:
- ✅ Completeness: +10-20% (explicit field requirements)
- ✅ Grounding: +15-25% (anti-hallucination rules)
- ✅ Correctness: +5-10% (fewer errors)
- ⚠️ Risk: Overly prescriptive may reduce flexibility

---

### Strategy 4: Simplify Language

**Purpose**: Reduce prompt verbosity to improve focus and reduce token usage.

**Implementation**:
```csharp
public class SimplifyLanguageStrategy : IPromptMutationStrategy
{
    public string Mutate(string basePrompt, MutationContext context)
    {
        return SimplifyText(basePrompt);
    }
    
    private string SimplifyText(string text)
    {
        // Remove redundant phrases
        // Replace complex words with simpler alternatives
        // Consolidate repetitive instructions
        // Remove unnecessary examples
    }
}
```

**Example Mutation**:
```markdown
Original:
---
You are an expert in data mapping and structured output transformation.
Your task is to intelligently map JSON input to a target schema using fuzzy 
logic and inference. Use intelligent inference to map input fields to schema 
fields. Apply fuzzy matching when field names don't exactly match. Infer 
appropriate data types based on schema requirements. Handle nested structures 
intelligently. Always produce output that conforms to the schema.

Simplified:
---
Map JSON input to the target schema.
- Use fuzzy matching for field names (e.g., "emailAddress" → "email")
- Infer types from schema requirements
- Handle nested structures
- Output must conform to schema
```

**Parameters**:
- `targetReduction`: Percentage of tokens to remove (default: 20%)
- `preserveKeyPhrases`: Critical phrases that must remain
- `minPromptLength`: Minimum acceptable prompt length

**Expected Improvements**:
- ✅ Cost: -20-30% (fewer tokens)
- ✅ Latency: -10-15% (faster processing)
- ⚠️ Quality: May decrease if oversimplified
- ⚠️ Risk: May lose important context

---

## Strategy Composition

### Sequential Application
Strategies can be chained:
```csharp
var optimizer = new IterativeOptimizer()
    .WithStrategy(new AddExamplesStrategy())
    .WithStrategy(new AddConstraintsStrategy());
```

### Parallel Exploration
Generate multiple independent variants:
```csharp
var variants = new[]
{
    new AddExamplesStrategy().Mutate(basePrompt, context),
    new RephraseInstructionsStrategy().Mutate(basePrompt, context),
    new AddConstraintsStrategy().Mutate(basePrompt, context)
};
```

### Adaptive Selection
Choose strategy based on current weaknesses:
```csharp
var strategy = SelectStrategyForWeakness(aggregatedMetrics);
// If Grounding is low → AddConstraintsStrategy
// If Correctness is low → AddExamplesStrategy
// If Relevance is low → RephraseInstructionsStrategy
```

## Mutation Context

```csharp
public class MutationContext
{
    public List<EvalTestCase> TestCases { get; set; }
    public List<TestCaseResult> FailedTestCases { get; set; }
    public AggregatedMetrics CurrentMetrics { get; set; }
    public string TargetImprovement { get; set; }  // e.g., "Grounding"
    public JsonElement Schema { get; set; }
}
```

## Strategy Selection Algorithm

```csharp
public class StrategySelector
{
    public IPromptMutationStrategy SelectStrategy(AggregatedMetrics metrics)
    {
        // Find lowest-scoring metric
        var weakestMetric = metrics.AverageScores.OrderBy(x => x.Value).First();
        
        return weakestMetric.Key switch
        {
            "Relevance" => new RephraseInstructionsStrategy(),
            "Correctness" => new AddExamplesStrategy(),
            "Completeness" => new AddConstraintsStrategy(
                focus: "field inclusion"),
            "Grounding" => new AddConstraintsStrategy(
                focus: "data preservation"),
            _ => new AddExamplesStrategy()  // Default
        };
    }
}
```

## Alternative Strategies Considered (Future)

### Genetic Algorithm Approach (Deferred)
**Approach**: Treat prompts as genomes, apply crossover and mutation.

**Pros**: Explores larger search space

**Cons**: 
- Requires many evaluations (expensive)
- Risk of incoherent prompts (crossover may break semantics)
- Complex to implement

**Verdict**: Deferred - too complex for initial implementation

### Reinforcement Learning (Deferred)
**Approach**: Learn mutation policy from optimization history.

**Pros**: Adaptive strategy selection over time

**Cons**:
- Requires extensive training data
- Computationally expensive
- Overkill for current problem scale

**Verdict**: Deferred - revisit after 100+ optimization runs

### Template-Based Generation (Rejected)
**Approach**: Define prompt templates with parameterized slots.

**Pros**: Structured, predictable

**Cons**:
- Limits creativity
- Requires manual template design
- Doesn't leverage LLM capabilities for generation

**Verdict**: Rejected - too rigid

## Implementation Plan

### Phase 1: Core Strategies
1. Implement `IPromptMutationStrategy` interface
2. Implement `AddExamplesStrategy`
3. Implement `AddConstraintsStrategy`
4. Unit tests for each strategy

### Phase 2: Advanced Strategies
5. Implement `RephraseInstructionsStrategy`
6. Implement `SimplifyLanguageStrategy`
7. Implement `StrategySelector`
8. Integration tests

### Phase 3: Composition
9. Build strategy chaining
10. Implement adaptive selection
11. Add mutation history tracking

## Evaluation Criteria

Each strategy will be evaluated on:
1. **Improvement Rate**: % of times it improves over baseline
2. **Improvement Magnitude**: Average Δ in composite score
3. **Consistency**: Standard deviation of results
4. **Cost**: Additional tokens and API calls
5. **Time**: Execution time per mutation

## Risks and Mitigations

### Risk 1: Strategy Conflicts
**Problem**: Constraints added by one strategy contradicted by another

**Mitigation**: 
- Define strategy precedence rules
- Detect and resolve conflicts automatically
- Allow manual review of combined strategies

### Risk 2: Prompt Bloat
**Problem**: Repeated application increases prompt size excessively

**Mitigation**:
- Set maximum prompt length (default: 4000 tokens)
- Use `SimplifyLanguageStrategy` periodically
- Remove redundant constraints

### Risk 3: Overfitting
**Problem**: Strategies optimize for specific test cases, not general quality

**Mitigation**:
- Use holdout test set for validation
- Rotate test cases regularly
- Monitor performance on new schemas

## Success Metrics

- [ ] All four core strategies implemented
- [ ] Each strategy improves composite score ≥60% of the time
- [ ] Average improvement magnitude ≥0.1 composite score
- [ ] Strategy selection based on weakness works
- [ ] No prompt exceeds token limits
- [ ] Comprehensive test coverage

## References

- [Few-Shot Learning](https://arxiv.org/abs/2005.14165)
- [Chain-of-Thought Prompting](https://arxiv.org/abs/2201.11903)
- [Constitutional AI](https://arxiv.org/abs/2212.08073)
- [Prompt Engineering Guide](https://www.promptingguide.ai/)

## Future Enhancements

1. **Meta-Learning**: Learn which strategies work best for which weaknesses
2. **Prompt Compression**: Use LLM to compress prompts while preserving intent
3. **Multi-Objective Optimization**: Balance quality, cost, and latency
4. **Human Feedback Integration**: Allow manual review and adjustment
5. **Schema-Specific Strategies**: Tailor mutations to schema characteristics
