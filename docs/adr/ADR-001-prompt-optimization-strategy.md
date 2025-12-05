# ADR-001: Prompt Optimization Strategy Selection

**Status**: Proposed  
**Date**: 2025-11-04  
**Deciders**: Development Team  
**Context Tags**: Architecture, AI/ML, Evaluation

## Context

AgenticStructuredOutput uses a fixed prompt (agent instructions) loaded from `Resources/agent-instructions.md` to perform JSON schema mapping. While the current prompt works reasonably well, we need an automated way to optimize prompts based on evaluation criteria and schema requirements. This is particularly important because:

1. **Diverse Schema Requirements**: Different schemas may benefit from different prompt strategies
2. **Evaluation Metrics**: We have existing LLM-based evaluation (Relevance, Correctness, Completeness, Grounding)
3. **Manual Optimization is Slow**: Currently requires editing markdown and manual testing
4. **No Feedback Loop**: No automated way to improve prompts based on evaluation results

## Decision Drivers

1. **Ecosystem Maturity**: Availability of libraries and tools
2. **Development Velocity**: Time to implement and iterate
3. **Maintenance Burden**: Long-term operational complexity
4. **Integration Complexity**: How well it fits with existing .NET architecture
5. **Performance**: Optimization runtime and efficiency
6. **User Preference**: Strong preference for .NET solutions

## Considered Options

### Option 1: .NET-Native Prompt Optimization (RECOMMENDED)

**Approach**: Build prompt optimization in C# using `Microsoft.Extensions.AI.Evaluation` framework that's already integrated.

**Architecture**:
```
┌─────────────────────────────────────────────────────────┐
│  AgenticStructuredOutput.Optimization (C# .NET 9.0)    │
├─────────────────────────────────────────────────────────┤
│  PromptOptimizer                                        │
│    ├─ IPromptMutationStrategy                          │
│    │    ├─ AddExamplesStrategy                         │
│    │    ├─ RephraseInstructionsStrategy                │
│    │    ├─ AddConstraintsStrategy                      │
│    │    └─ SimplifyLanguageStrategy                    │
│    ├─ IEvaluationAggregator                            │
│    │    └─ Aggregates scores across test cases         │
│    └─ IterativeOptimizer                               │
│         └─ Mutation → Eval → Compare → Select loop     │
├─────────────────────────────────────────────────────────┤
│  Uses: Microsoft.Extensions.AI.Evaluation               │
│        JsonSchema.Net for validation                    │
│        Existing AgenticStructuredOutput.Tests harness   │
└─────────────────────────────────────────────────────────┘
```

**Pros**:
- ✅ **Native Integration**: Uses existing .NET infrastructure and evaluation framework
- ✅ **No Language Barrier**: Pure C# - easier maintenance
- ✅ **Single Deployment**: No Python runtime required
- ✅ **Type Safety**: Compile-time checking
- ✅ **Existing Eval Framework**: Leverage `Microsoft.Extensions.AI.Evaluation` already in use
- ✅ **Minimal Dependencies**: Builds on what's already there
- ✅ **GitHub Actions Compatible**: No additional runtime setup needed
- ✅ **Aligns with User Preference**: Strong preference for .NET

**Cons**:
- ❌ **Less Mature Ecosystem**: .NET prompt optimization tools less mature than Python (DSPy)
- ❌ **Custom Implementation**: Need to build optimization algorithms from scratch
- ❌ **Fewer Examples**: Less community knowledge/examples compared to DSPy

**Implementation Complexity**: Medium (2-3 weeks)

### Option 2: Python DSPy Integration

**Approach**: Use [DSPy](https://github.com/stanfordnlp/dspy) (Stanford) for prompt optimization via Python subprocess or REST API.

**Architecture**:
```
┌──────────────────────────────────────────────────────┐
│  AgenticStructuredOutput.Optimization (C# Wrapper)   │
│    └─ Calls Python subprocess or HTTP API            │
└──────────────────────────────────────────────────────┘
         ↓
┌──────────────────────────────────────────────────────┐
│  Python DSPy Service                                 │
│    ├─ dspy.teleprompt.BootstrapFewShot              │
│    ├─ dspy.teleprompt.MIPRO                         │
│    └─ Custom evaluation metrics                     │
└──────────────────────────────────────────────────────┘
```

**Pros**:
- ✅ **Mature Library**: DSPy is well-established for prompt optimization
- ✅ **Proven Algorithms**: Bootstrap, MIPRO, etc. are research-backed
- ✅ **Rich Ecosystem**: Lots of community examples and patterns
- ✅ **Advanced Features**: Few-shot learning, teleprompters, etc.

**Cons**:
- ❌ **Language Barrier**: Python ↔ .NET interop complexity
- ❌ **Additional Runtime**: Requires Python 3.9+ in production
- ❌ **Deployment Complexity**: Two runtimes to manage (Docker, etc.)
- ❌ **Serialization Overhead**: JSON/gRPC between processes
- ❌ **Debugging Difficulty**: Cross-language debugging is hard
- ❌ **Against User Preference**: Violates strong .NET preference
- ❌ **CI/CD Complexity**: GitHub Actions needs Python setup
- ❌ **Maintenance Split**: Team needs Python + .NET expertise

**Implementation Complexity**: High (4-5 weeks, including interop)

### Option 3: Agent-Lightning (Python)

**Approach**: Use [Microsoft's Agent-Lightning](https://github.com/microsoft/agent-lightning) framework for agentic optimization.

**Architecture**: Similar to DSPy but with Microsoft's framework.

**Pros**:
- ✅ **Microsoft Ecosystem**: Official Microsoft tool
- ✅ **Designed for Agents**: Built specifically for agent optimization
- ✅ **Integration with Azure**: Good Azure AI integration

**Cons**:
- ❌ **All DSPy Cons Apply**: Same interop, deployment, maintenance issues
- ❌ **Less Mature than DSPy**: Newer library, less community adoption
- ❌ **Against User Preference**: Still requires Python runtime

**Implementation Complexity**: High (4-5 weeks)

### Option 4: Hybrid Approach (Python for Optimization, .NET for Execution)

**Approach**: Use Python DSPy **only during development** for optimization, then export optimized prompts for .NET runtime.

**Architecture**:
```
Development Time:
  Python DSPy → Optimize Prompt → Export to .md file

Runtime:
  .NET Agent → Loads optimized prompt → Executes with Azure AI
```

**Pros**:
- ✅ **Best of Both Worlds**: DSPy benefits without runtime Python
- ✅ **Clean Separation**: Optimization is dev-time tool, not production dependency
- ✅ **Simpler Deployment**: Only .NET in production
- ✅ **Faster Execution**: No interop at runtime

**Cons**:
- ❌ **Manual Export Step**: Need to run Python tool to optimize
- ❌ **No Runtime Adaptation**: Can't optimize prompts in production
- ❌ **Dev Environment Complexity**: Developers need Python setup
- ❌ **Versioning Challenges**: Tracking prompt version history across languages

**Implementation Complexity**: Medium-High (3-4 weeks)

## Decision

**RECOMMENDED: Option 1 - .NET-Native Prompt Optimization**

### Rationale

1. **Aligns with User Preference**: "Very strong preference for doing this in dotnet"
2. **Simpler Architecture**: Single language, single runtime, easier maintenance
3. **Existing Foundation**: We already have `Microsoft.Extensions.AI.Evaluation` integrated
4. **Practical Optimization Needs**: Our use case (schema mapping) is straightforward - we don't need advanced DSPy features
5. **Sufficient Evaluation Framework**: LLM judges (Relevance, Correctness, etc.) already give us quality signals
6. **Manageable Implementation**: Can build iterative optimizer with mutation strategies in reasonable timeframe

### What We're Trading Off

We acknowledge that DSPy has more mature optimization algorithms. However:
- Our domain (JSON schema mapping) is simpler than general prompt optimization
- We can implement effective strategies (add examples, rephrase, constrain) without advanced ML
- Integration and operational simplicity outweigh algorithmic sophistication
- We can always revisit if .NET approach proves insufficient

### Case Against Python Options

To use Python (DSPy/Agent-Lightning), we would need to demonstrate that:
1. .NET approach fundamentally cannot achieve acceptable optimization quality
2. The additional complexity (interop, dual runtimes, deployment) is justified by measurably superior results
3. Team is willing to accept split technology stack maintenance burden

**None of these conditions are met for our use case.**

## Implementation Plan

### Phase 1: Core Optimization Framework (Week 1)
1. Create `AgenticStructuredOutput.Optimization` project
2. Implement `IPromptOptimizer` interface
3. Build `PromptMutationStrategy` base classes
4. Create `EvaluationAggregator` for metrics

### Phase 2: Mutation Strategies (Week 2)
1. Implement `AddExamplesStrategy` (inject few-shot examples)
2. Implement `RephraseInstructionsStrategy` (use LLM to rephrase)
3. Implement `AddConstraintsStrategy` (add specific rules)
4. Implement `SimplifyLanguageStrategy` (reduce verbosity)

### Phase 3: Optimization Loop (Week 3)
1. Build `IterativeOptimizer` with hill-climbing algorithm
2. Implement prompt history and versioning
3. Add CLI tool for running optimization experiments
4. Create comprehensive tests

### Phase 4: Documentation & Integration
1. Document optimization strategies and usage
2. Integrate with existing test harness
3. Create examples and best practices guide

## Consequences

### Positive
- Single technology stack for entire solution
- Easier for .NET developers to contribute
- Simpler CI/CD pipeline
- No Python runtime in production
- Can iterate quickly on optimization strategies
- Type-safe implementation

### Negative
- Need to build optimization algorithms from scratch
- May not match DSPy's algorithmic sophistication initially
- Smaller community to learn from (less .NET prompt optimization examples)

### Mitigation Strategies
1. Start with simple, proven strategies (hill-climbing, mutation testing)
2. Leverage existing evaluation framework rather than building new one
3. Document learnings to build institutional knowledge
4. Monitor optimization quality and be willing to revisit decision if needed

## References

- [DSPy Documentation](https://dspy-docs.vercel.app/)
- [Microsoft.Extensions.AI.Evaluation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.evaluation)
- [Prompt Engineering Guide](https://www.promptingguide.ai/)
- [Agent-Lightning](https://github.com/microsoft/agent-lightning)

## Future Considerations

- If .NET optimization proves insufficient, consider **Option 4 (Hybrid)** as next step
- Monitor .NET ecosystem for emerging prompt optimization libraries
- Consider contributing our implementation back to .NET community
- Evaluate if Azure AI Foundry adds relevant optimization capabilities
