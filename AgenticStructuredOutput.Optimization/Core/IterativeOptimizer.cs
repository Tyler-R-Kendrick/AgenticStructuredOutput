using AgenticStructuredOutput.Optimization.Core;
using AgenticStructuredOutput.Optimization.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AgenticStructuredOutput.Optimization.Core;

/// <summary>
/// Implements hill-climbing optimization with random restarts and adaptive strategy selection.
/// </summary>
public class IterativeOptimizer(
    IEvaluationAggregator evaluationAggregator,
    IEnumerable<IPromptMutationStrategy> strategies,
    ILogger<IterativeOptimizer> logger) : IPromptOptimizer
{
    private readonly IEvaluationAggregator _evaluationAggregator = evaluationAggregator;
    private readonly IEnumerable<IPromptMutationStrategy> _strategies = strategies;
    private readonly ILogger<IterativeOptimizer> _logger = logger;

    public async Task<OptimizationResult> OptimizeAsync(
        string baselinePrompt,
        IEnumerable<EvalTestCase> testCases,
        OptimizationConfig config,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation("Starting prompt optimization with {MaxIterations} max iterations", 
            config.MaxIterations);

        var testCaseList = testCases.ToList();
        
        // Evaluate baseline
        _logger.LogInformation("Evaluating baseline prompt...");
        var baselineMetrics = await _evaluationAggregator.EvaluatePromptAsync(
            baselinePrompt,
            testCaseList,
            config,
            cancellationToken);

        _logger.LogInformation(
            "Baseline: Composite={Composite:F2}, PassRate={PassRate:P0}",
            baselineMetrics.CompositeScore,
            baselineMetrics.PassRate);

        var result = new OptimizationResult
        {
            BaselineMetrics = baselineMetrics,
            BestPrompt = baselinePrompt,
            BestMetrics = baselineMetrics,
            Config = config,
            StartTime = startTime
        };

        var currentPrompt = baselinePrompt;
        var currentMetrics = baselineMetrics;
        var iterationsWithoutImprovement = 0;
        var enabledStrategies = _strategies
            .Where(s => config.EnabledStrategies.Contains(s.Name))
            .ToList();

        if (enabledStrategies.Count == 0)
        {
            _logger.LogWarning("No strategies enabled, using all available strategies");
            enabledStrategies = _strategies.ToList();
        }

        // Optimization loop
        for (int i = 0; i < config.MaxIterations; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.StoppingReason = "Cancelled";
                break;
            }

            _logger.LogInformation("Iteration {Iteration}/{MaxIterations}", i + 1, config.MaxIterations);

            var iterationStart = DateTime.UtcNow;

            // Select strategies based on current weaknesses
            var selectedStrategies = SelectStrategiesForWeaknesses(
                currentMetrics,
                enabledStrategies,
                config);

            // Generate candidates
            var candidates = await GenerateCandidatesAsync(
                currentPrompt,
                selectedStrategies,
                testCaseList,
                currentMetrics,
                cancellationToken);

            // Evaluate candidates
            var candidateEvaluations = new List<(string Prompt, string Strategy, AggregatedMetrics Metrics)>();
            
            foreach (var (prompt, strategy) in candidates)
            {
                try
                {
                    var metrics = await _evaluationAggregator.EvaluatePromptAsync(
                        prompt,
                        testCaseList,
                        config,
                        cancellationToken);

                    candidateEvaluations.Add((prompt, strategy, metrics));
                    
                    _logger.LogInformation(
                        "  Strategy '{Strategy}': Composite={Composite:F2} (Δ={Delta:+F2;-F2})",
                        strategy,
                        metrics.CompositeScore,
                        metrics.CompositeScore - currentMetrics.CompositeScore);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to evaluate candidate from strategy '{Strategy}'", strategy);
                }
            }

            if (candidateEvaluations.Count == 0)
            {
                _logger.LogWarning("No candidates could be evaluated, stopping optimization");
                result.StoppingReason = "No valid candidates";
                break;
            }

            // Find best candidate
            var (bestPrompt, bestStrategy, bestMetrics) = candidateEvaluations
                .OrderByDescending(c => c.Metrics.CompositeScore)
                .First();

            var improvement = bestMetrics.CompositeScore - currentMetrics.CompositeScore;

            // Record iteration
            var iteration = new OptimizationIteration
            {
                IterationNumber = i + 1,
                StrategyUsed = bestStrategy,
                CandidatePrompt = bestPrompt,
                BestCandidateScore = bestMetrics.CompositeScore,
                Improvement = improvement,
                CandidateMetrics = bestMetrics,
                Timestamp = iterationStart,
                Duration = DateTime.UtcNow - iterationStart
            };

            // Decide whether to accept
            bool accepted = false;

            if (improvement > config.ImprovementThreshold)
            {
                // Clear improvement
                _logger.LogInformation("✓ Accepted: Improvement of {Improvement:+F2}", improvement);
                currentPrompt = bestPrompt;
                currentMetrics = bestMetrics;
                iterationsWithoutImprovement = 0;
                accepted = true;

                // Update best if this is the best so far
                if (bestMetrics.CompositeScore > result.BestMetrics.CompositeScore)
                {
                    result.BestPrompt = bestPrompt;
                    result.BestMetrics = bestMetrics;
                }
            }
            else if (improvement > config.LateralMoveThreshold && 
                     Random.Shared.NextDouble() < config.LateralMoveProbability)
            {
                // Small regression/lateral move accepted probabilistically
                _logger.LogInformation(
                    "~ Lateral move accepted: {Improvement:+F2} (within threshold)", 
                    improvement);
                currentPrompt = bestPrompt;
                currentMetrics = bestMetrics;
                iterationsWithoutImprovement++;
                accepted = true;
            }
            else
            {
                _logger.LogInformation("✗ Rejected: No significant improvement ({Improvement:+F2})", improvement);
                iterationsWithoutImprovement++;
            }

            iteration.Accepted = accepted;
            result.History.Add(iteration);

            // Early stopping if excellent score
            if (currentMetrics.CompositeScore >= config.EarlyStoppingScore)
            {
                _logger.LogInformation(
                    "Early stopping: Score {Score:F2} >= threshold {Threshold:F2}",
                    currentMetrics.CompositeScore,
                    config.EarlyStoppingScore);
                result.StoppingReason = "Early stopping (excellent score)";
                break;
            }

            // Random restart if stuck
            if (iterationsWithoutImprovement >= config.RestartAfterStuckIterations)
            {
                _logger.LogInformation(
                    "Random restart: No improvement for {Count} iterations",
                    iterationsWithoutImprovement);

                var randomStrategy = enabledStrategies[Random.Shared.Next(enabledStrategies.Count)];
                try
                {
                    currentPrompt = await randomStrategy.MutateAsync(
                        baselinePrompt,
                        new MutationContext
                        {
                            TestCases = testCaseList,
                            CurrentMetrics = currentMetrics
                        });
                    iterationsWithoutImprovement = 0;
                    iteration.WasRestart = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Random restart failed");
                }
            }
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        result.EndTime = DateTime.UtcNow;

        if (string.IsNullOrEmpty(result.StoppingReason))
        {
            result.StoppingReason = "Max iterations reached";
        }

        _logger.LogInformation(
            "Optimization complete: Best={Best:F2}, Baseline={Baseline:F2}, Improvement={Improvement:+F2}, Reason={Reason}",
            result.BestMetrics.CompositeScore,
            result.BaselineMetrics.CompositeScore,
            result.TotalImprovement,
            result.StoppingReason);

        return result;
    }

    private List<IPromptMutationStrategy> SelectStrategiesForWeaknesses(
        AggregatedMetrics metrics,
        List<IPromptMutationStrategy> availableStrategies,
        OptimizationConfig config)
    {
        // Find the weakest metric
        var weakestMetric = metrics.AverageScores
            .OrderBy(kvp => kvp.Value)
            .FirstOrDefault();

        // Select strategy based on weakness
        var targetedStrategy = weakestMetric.Key switch
        {
            "Relevance" => availableStrategies.FirstOrDefault(s => s.Name == "RephraseInstructions"),
            "Correctness" => availableStrategies.FirstOrDefault(s => s.Name == "AddExamples"),
            "Completeness" => availableStrategies.FirstOrDefault(s => s.Name == "AddConstraints"),
            "Grounding" => availableStrategies.FirstOrDefault(s => s.Name == "AddConstraints"),
            _ => null
        };

        var selectedStrategies = new List<IPromptMutationStrategy>();
        
        if (targetedStrategy != null)
        {
            selectedStrategies.Add(targetedStrategy);
        }

        // Add other strategies for exploration
        selectedStrategies.AddRange(
            availableStrategies
                .Where(s => !selectedStrategies.Contains(s))
                .Take(3 - selectedStrategies.Count));

        return selectedStrategies;
    }

    private async Task<List<(string Prompt, string Strategy)>> GenerateCandidatesAsync(
        string currentPrompt,
        List<IPromptMutationStrategy> strategies,
        List<EvalTestCase> testCases,
        AggregatedMetrics currentMetrics,
        CancellationToken cancellationToken)
    {
        var candidates = new List<(string, string)>();
        var context = new MutationContext
        {
            TestCases = testCases,
            CurrentMetrics = currentMetrics
        };

        foreach (var strategy in strategies)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var mutated = await strategy.MutateAsync(currentPrompt, context);
                candidates.Add((mutated, strategy.Name));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Strategy '{Strategy}' failed to generate candidate", strategy.Name);
            }
        }

        return candidates;
    }
}
