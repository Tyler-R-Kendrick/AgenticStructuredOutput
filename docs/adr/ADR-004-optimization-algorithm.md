# ADR-004: Optimization Algorithm Selection

**Status**: Proposed  
**Date**: 2025-11-04  
**Deciders**: Development Team  
**Context Tags**: Algorithms, Optimization, Performance

## Context

We need to select an optimization algorithm that:
1. Efficiently explores the prompt space
2. Converges to good solutions quickly (low API cost)
3. Avoids local minima
4. Is simple to implement and understand
5. Works well with discrete/textual search space (prompts)

## Problem Characteristics

- **Search Space**: Discrete, high-dimensional (all possible prompts)
- **Objective Function**: Composite score from LLM evaluators (noisy)
- **Evaluation Cost**: Expensive (~$0.005-$0.01 per prompt evaluation)
- **Time Constraints**: Should complete in <5 minutes for 10 iterations
- **Noise**: LLM judges may have variance in scoring
- **No Gradient Information**: Can't compute derivatives of score w.r.t. prompt text

This is a **derivative-free, discrete, noisy optimization** problem.

## Considered Algorithms

### Option 1: Hill Climbing with Random Restarts (RECOMMENDED)

**Description**: 
- Start with baseline prompt
- Generate mutation variants using strategies
- Evaluate each variant
- Keep best variant if improved
- Repeat until no improvement or max iterations
- Random restart if stuck

**Pseudocode**:
```python
best_prompt = baseline_prompt
best_score = evaluate(best_prompt)

for iteration in range(max_iterations):
    # Generate candidates
    candidates = [
        strategy.mutate(best_prompt) 
        for strategy in strategies
    ]
    
    # Evaluate candidates
    scores = [evaluate(c) for c in candidates]
    
    # Select best
    best_candidate_idx = argmax(scores)
    if scores[best_candidate_idx] > best_score + threshold:
        best_prompt = candidates[best_candidate_idx]
        best_score = scores[best_candidate_idx]
    else:
        # Stuck - random restart
        best_prompt = generate_random_variant(baseline_prompt)
```

**Pros**:
- ✅ Simple to implement and understand
- ✅ Fast convergence (few iterations needed)
- ✅ Works well with expensive evaluations
- ✅ No hyperparameters to tune (beyond threshold)
- ✅ Handles noise reasonably well

**Cons**:
- ❌ Can get stuck in local minima
- ❌ No memory of search history
- ❌ Greedy (may miss better solutions)

**Mitigations**:
- Random restarts to escape local minima
- Multiple strategies to explore different directions
- Allow occasional lateral moves (accept if ΔScore > -0.02)

**Estimated Performance**:
- Convergence: 5-10 iterations typically
- API Calls: ~4 strategies × 10 iterations = 40 evaluations
- Time: ~2-3 minutes
- Cost: ~$0.20-$0.40

---

### Option 2: Beam Search

**Description**:
- Maintain top-k prompt candidates ("beam")
- Generate mutations for each candidate
- Keep top-k from combined pool
- Iterate until convergence

**Pseudocode**:
```python
beam = [baseline_prompt]
beam_scores = [evaluate(baseline_prompt)]
k = 3  # Beam width

for iteration in range(max_iterations):
    candidates = []
    for prompt in beam:
        candidates.extend([
            strategy.mutate(prompt) 
            for strategy in strategies
        ])
    
    scores = [evaluate(c) for c in candidates]
    top_k_indices = argsort(scores)[-k:]
    beam = [candidates[i] for i in top_k_indices]
    beam_scores = [scores[i] for i in top_k_indices]
```

**Pros**:
- ✅ Explores multiple paths simultaneously
- ✅ Less likely to get stuck in local minima
- ✅ Maintains diversity in candidate pool

**Cons**:
- ❌ More expensive (k × strategies evaluations per iteration)
- ❌ Slower convergence (more iterations needed)
- ❌ Requires tuning beam width k
- ❌ Overkill for current problem scale

**Estimated Performance**:
- Convergence: 10-15 iterations
- API Calls: ~3 beam × 4 strategies × 15 iterations = 180 evaluations
- Time: ~8-10 minutes
- Cost: ~$0.90-$1.80

**Verdict**: Too expensive for marginal benefit over hill climbing.

---

### Option 3: Bayesian Optimization

**Description**:
- Model score as function of prompt features
- Use Gaussian Process or similar to predict scores
- Select next prompt to evaluate based on acquisition function

**Pros**:
- ✅ Sample efficient (fewer evaluations needed)
- ✅ Principled uncertainty handling
- ✅ Works well with expensive black-box functions

**Cons**:
- ❌ Complex to implement
- ❌ Requires feature extraction from prompts (embeddings)
- ❌ Assumes continuous, smooth objective (prompts are discrete)
- ❌ Hyperparameter tuning required

**Verdict**: Overkill - better for continuous spaces and when evaluations are very expensive (>$1 each).

---

### Option 4: Genetic Algorithm

**Description**:
- Population of prompt variants
- Crossover: combine parts of two prompts
- Mutation: apply strategies
- Selection: keep best prompts

**Pros**:
- ✅ Explores diverse solutions
- ✅ Can escape local minima
- ✅ Parallelizable

**Cons**:
- ❌ Crossover often produces incoherent prompts (text isn't DNA)
- ❌ Requires large population (expensive)
- ❌ Slow convergence
- ❌ Many hyperparameters (population size, mutation rate, etc.)

**Verdict**: Rejected - crossover doesn't work well for natural language prompts.

---

### Option 5: Simulated Annealing

**Description**:
- Start with baseline
- Generate random mutation
- Accept improvement always, accept regression with probability exp(-ΔScore/temperature)
- Decrease temperature over time

**Pros**:
- ✅ Can escape local minima
- ✅ Simple to implement
- ✅ Proven effective for discrete optimization

**Cons**:
- ❌ Requires careful temperature schedule tuning
- ❌ May waste evaluations on bad prompts (accepted due to temperature)
- ❌ Slower than hill climbing

**Verdict**: Good alternative, but hill climbing with random restarts is simpler and likely sufficient.

---

## Decision

**SELECTED: Hill Climbing with Random Restarts and Adaptive Strategy Selection**

### Enhanced Algorithm

```csharp
public class IterativeOptimizer : IPromptOptimizer
{
    public async Task<OptimizationResult> OptimizeAsync(
        string baselinePrompt,
        IEnumerable<EvalTestCase> testCases,
        OptimizationConfig config)
    {
        var history = new List<OptimizationIteration>();
        var bestPrompt = baselinePrompt;
        var bestMetrics = await EvaluatePromptAsync(bestPrompt, testCases);
        
        int iterationsWithoutImprovement = 0;
        
        for (int i = 0; i < config.MaxIterations; i++)
        {
            // Adaptive strategy selection based on weakest metric
            var strategies = SelectStrategiesForWeaknesses(bestMetrics);
            
            // Generate candidates
            var candidates = strategies
                .Select(s => s.Mutate(bestPrompt, new MutationContext
                {
                    TestCases = testCases,
                    CurrentMetrics = bestMetrics
                }))
                .ToList();
            
            // Evaluate candidates in parallel
            var candidateMetrics = await Task.WhenAll(
                candidates.Select(c => EvaluatePromptAsync(c, testCases)));
            
            // Find best candidate
            var bestCandidateIdx = candidateMetrics
                .Select((m, idx) => (m.CompositeScore, idx))
                .OrderByDescending(x => x.CompositeScore)
                .First().idx;
            
            var candidateScore = candidateMetrics[bestCandidateIdx].CompositeScore;
            var improvement = candidateScore - bestMetrics.CompositeScore;
            
            // Record iteration
            history.Add(new OptimizationIteration
            {
                IterationNumber = i + 1,
                BestCandidateScore = candidateScore,
                Improvement = improvement,
                StrategyUsed = strategies[bestCandidateIdx].Name
            });
            
            // Accept if improvement
            if (improvement > config.ImprovementThreshold)
            {
                bestPrompt = candidates[bestCandidateIdx];
                bestMetrics = candidateMetrics[bestCandidateIdx];
                iterationsWithoutImprovement = 0;
            }
            // Allow small lateral moves to avoid getting stuck
            else if (improvement > -0.02 && Random.Shared.NextDouble() < 0.1)
            {
                bestPrompt = candidates[bestCandidateIdx];
                bestMetrics = candidateMetrics[bestCandidateIdx];
                iterationsWithoutImprovement++;
            }
            else
            {
                iterationsWithoutImprovement++;
            }
            
            // Random restart if stuck
            if (iterationsWithoutImprovement >= 3)
            {
                var randomStrategy = strategies[Random.Shared.Next(strategies.Count)];
                bestPrompt = randomStrategy.Mutate(baselinePrompt, new MutationContext
                {
                    TestCases = testCases,
                    CurrentMetrics = bestMetrics
                });
                iterationsWithoutImprovement = 0;
            }
            
            // Early stopping if very good
            if (bestMetrics.CompositeScore >= 4.5)
                break;
        }
        
        return new OptimizationResult
        {
            BestPrompt = bestPrompt,
            BestMetrics = bestMetrics,
            History = history
        };
    }
}
```

### Key Features

1. **Adaptive Strategy Selection**: Choose strategies that target current weaknesses
2. **Parallel Evaluation**: Evaluate all candidates simultaneously (faster)
3. **Lateral Moves**: Occasionally accept non-improvements to explore (10% chance)
4. **Random Restarts**: Restart from baseline if stuck for 3 iterations
5. **Early Stopping**: Stop if score ≥ 4.5 (excellent quality)

### Configuration Defaults

```csharp
public class OptimizationConfig
{
    public int MaxIterations { get; set; } = 10;
    public double ImprovementThreshold { get; set; } = 0.05;  // 5% improvement
    public double LateralMoveThreshold { get; set; } = -0.02;
    public double LateralMoveProbability { get; set; } = 0.1;  // 10% chance
    public int RestartAfterStuckIterations { get; set; } = 3;
    public double EarlyStoppingScore { get; set; } = 4.5;
    
    public List<string> EnabledStrategies { get; set; } = new()
    {
        "AddExamples",
        "AddConstraints",
        "RephraseInstructions",
        "SimplifyLanguage"
    };
    
    public Dictionary<string, double> MetricWeights { get; set; } = new()
    {
        { "Relevance", 1.0 },
        { "Correctness", 1.5 },
        { "Completeness", 1.0 },
        { "Grounding", 1.25 }
    };
}
```

## Performance Characteristics

### Expected Convergence Profile

```
Iteration 1: +0.3 (large improvement from examples)
Iteration 2: +0.2 (constraints help)
Iteration 3: +0.1 (incremental improvement)
Iteration 4: +0.05 (diminishing returns)
Iteration 5: +0.02 (near convergence)
Iteration 6-10: <0.01 (converged)
```

### Resource Usage

| Metric | Value |
|--------|-------|
| Avg Iterations to Convergence | 5-7 |
| API Calls per Iteration | 4 strategies × 10 test cases = 40 |
| Total API Calls | ~200-280 |
| Time per Iteration | ~20-30 seconds |
| Total Time | ~2-4 minutes |
| Cost per Run | ~$0.20-$0.40 |

## Alternative Algorithm (Future Consideration)

### Ensemble Approach
Combine multiple algorithms:
- Hill climbing for fast convergence
- Occasional beam search for exploration
- Simulated annealing when stuck

This could be implemented if single-algorithm approach proves insufficient.

## Risks and Mitigations

### Risk 1: Premature Convergence
**Problem**: Algorithm stops at local optimum

**Mitigation**:
- Random restarts every N iterations
- Lateral move acceptance
- Multiple independent runs (compare results)

### Risk 2: Noisy Evaluations
**Problem**: LLM judge variance causes suboptimal decisions

**Mitigation**:
- Require improvement > threshold (not just > 0)
- Average scores across multiple test cases
- Consider re-evaluating promising candidates

### Risk 3: Expensive Evaluation
**Problem**: Too many API calls, high cost

**Mitigation**:
- Parallel evaluation of candidates
- Early stopping when score is excellent
- Cache evaluations for identical prompts
- Provide "fast mode" with fewer test cases

## Success Criteria

- [ ] Optimization completes in <5 minutes
- [ ] Achieves >10% improvement over baseline ≥70% of the time
- [ ] Cost per run <$0.50
- [ ] Converges in ≤10 iterations typically
- [ ] Does not get stuck in local minima >50% of the time
- [ ] Produces coherent, usable prompts (not gibberish)

## Monitoring and Metrics

Track these metrics for each optimization run:
- Iterations until convergence
- Final composite score
- Improvement over baseline
- Strategies that worked best
- Total API cost
- Total time

Use this data to refine algorithm and strategy selection over time.

## References

- [Hill Climbing Algorithm](https://en.wikipedia.org/wiki/Hill_climbing)
- [Derivative-Free Optimization](https://arxiv.org/abs/1904.11585)
- [Prompt Optimization Techniques](https://arxiv.org/abs/2305.03495)
- [Bayesian Optimization for LLMs](https://arxiv.org/abs/2305.20009)

## Future Enhancements

1. **Multi-Start Optimization**: Run multiple hill climbs from different starting points
2. **Transfer Learning**: Use results from previous schemas to warm-start
3. **Meta-Learning**: Learn which strategies work best for which problems
4. **Portfolio Approach**: Combine multiple algorithms with different strengths
5. **Online Learning**: Continuously optimize prompts based on production usage

## Acceptance Criteria

- [ ] `IterativeOptimizer` class implemented
- [ ] Adaptive strategy selection working
- [ ] Random restarts functional
- [ ] Parallel evaluation implemented
- [ ] Early stopping logic in place
- [ ] Configuration options working
- [ ] Comprehensive unit tests
- [ ] Performance meets targets (time, cost)
- [ ] Integration tests with full pipeline
