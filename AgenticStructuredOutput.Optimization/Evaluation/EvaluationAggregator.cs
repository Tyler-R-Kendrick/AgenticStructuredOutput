using AgenticStructuredOutput.Optimization.Core;
using AgenticStructuredOutput.Optimization.Models;
using AgenticStructuredOutput.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AgenticStructuredOutput.Optimization.Evaluation;

/// <summary>
/// Aggregates evaluation metrics across multiple test cases.
/// </summary>
public class EvaluationAggregator : IEvaluationAggregator
{
    private readonly IAgentFactory _agentFactory;
    private readonly IChatClient _judgeClient;
    private readonly ILogger<EvaluationAggregator> _logger;

    public EvaluationAggregator(
        IAgentFactory agentFactory,
        IChatClient judgeClient,
        ILogger<EvaluationAggregator> logger)
    {
        _agentFactory = agentFactory;
        _judgeClient = judgeClient;
        _logger = logger;
    }

    public async Task<AggregatedMetrics> EvaluatePromptAsync(
        string prompt,
        IEnumerable<EvalTestCase> testCases,
        OptimizationConfig config,
        CancellationToken cancellationToken = default)
    {
        var testCaseList = testCases.ToList();
        _logger.LogInformation("Evaluating prompt across {Count} test cases", testCaseList.Count);

        var metricsByType = new Dictionary<string, List<double>>();
        var passedCount = 0;

        // Create evaluators
        var evaluators = new Dictionary<string, IEvaluator>
        {
            ["Relevance"] = new RelevanceEvaluator(),
            ["Correctness"] = new EquivalenceEvaluator(),
            ["Completeness"] = new CompletenessEvaluator(),
            ["Grounding"] = new GroundednessEvaluator()
        };

        // Evaluate each test case
        var tasks = testCaseList.Select(async testCase =>
        {
            try
            {
                // Invoke agent with the prompt
                var response = await InvokeAgentWithPromptAsync(
                    prompt,
                    testCase,
                    cancellationToken);

                // Evaluate with LLM judges
                var scores = await EvaluateResponseAsync(
                    testCase.Input,
                    response,
                    evaluators,
                    cancellationToken);

                return (TestCase: testCase, Scores: scores, Success: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evaluate test case {Id}", testCase.Id);
                return (TestCase: testCase, Scores: new Dictionary<string, double>(), Success: false);
            }
        });

        IEnumerable<(EvalTestCase TestCase, Dictionary<string, double> Scores, bool Success)> results;
        
        if (config.ParallelEvaluation)
        {
            // Run in parallel with throttling
            using var semaphore = new SemaphoreSlim(config.MaxParallelTasks);
            var throttledTasks = tasks.Select(async task =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await task;
                }
                finally
                {
                    semaphore.Release();
                }
            });
            results = await Task.WhenAll(throttledTasks);
        }
        else
        {
            // Run sequentially
            var resultList = new List<(EvalTestCase, Dictionary<string, double>, bool)>();
            foreach (var task in tasks)
            {
                resultList.Add(await task);
            }
            results = resultList;
        }

        // Aggregate scores
        foreach (var (testCase, scores, success) in results)
        {
            if (!success) continue;

            // Check if test case passed (all scores >= 3.0)
            var passed = scores.Values.All(s => s >= 3.0);
            if (passed) passedCount++;

            // Add to metric collections
            foreach (var (metricName, score) in scores)
            {
                if (!metricsByType.ContainsKey(metricName))
                {
                    metricsByType[metricName] = new List<double>();
                }
                metricsByType[metricName].Add(score);
            }
        }

        // Calculate aggregated metrics
        var aggregatedMetrics = new AggregatedMetrics
        {
            PromptText = prompt,
            TestCaseCount = testCaseList.Count,
            PassedTestCases = passedCount
        };

        foreach (var (metricName, scoreList) in metricsByType)
        {
            if (scoreList.Count == 0) continue;

            var average = scoreList.Average();
            var min = scoreList.Min();
            var max = scoreList.Max();
            var stdDev = CalculateStdDev(scoreList, average);

            aggregatedMetrics.AverageScores[metricName] = average;
            aggregatedMetrics.MinScores[metricName] = min;
            aggregatedMetrics.MaxScores[metricName] = max;
            aggregatedMetrics.StdDeviation[metricName] = stdDev;
        }

        // Calculate composite score (weighted average)
        aggregatedMetrics.CompositeScore = CalculateCompositeScore(
            aggregatedMetrics.AverageScores,
            config.MetricWeights);

        _logger.LogInformation(
            "Evaluation complete: Composite={Composite:F2}, PassRate={PassRate:P0}",
            aggregatedMetrics.CompositeScore,
            aggregatedMetrics.PassRate);

        return aggregatedMetrics;
    }

    public PromptComparison ComparePrompts(
        AggregatedMetrics baseline,
        AggregatedMetrics candidate,
        OptimizationConfig config)
    {
        var comparison = new PromptComparison
        {
            Baseline = baseline,
            Candidate = candidate,
            DeltaCompositeScore = candidate.CompositeScore - baseline.CompositeScore
        };

        // Calculate deltas for each metric
        foreach (var metricName in baseline.AverageScores.Keys)
        {
            var baselineScore = baseline.AverageScores[metricName];
            var candidateScore = candidate.AverageScores.GetValueOrDefault(metricName, 0);
            var delta = candidateScore - baselineScore;

            comparison.DeltaByMetric[metricName] = delta;

            if (delta > 0.01) // Improved
            {
                comparison.ImprovedMetrics.Add(metricName);
            }
            else if (delta < -0.01) // Regressed
            {
                comparison.RegressedMetrics.Add(metricName);
            }
        }

        // Determine if it's an improvement
        comparison.IsImprovement = 
            comparison.DeltaCompositeScore > config.ImprovementThreshold &&
            !comparison.RegressedMetrics.Any(m => 
                comparison.DeltaByMetric[m] < -0.5); // No major regressions

        // Generate summary
        comparison.Summary = GenerateSummary(comparison);

        return comparison;
    }

    private async Task<string> InvokeAgentWithPromptAsync(
        string prompt,
        EvalTestCase testCase,
        CancellationToken cancellationToken)
    {
        if (!testCase.Schema.HasValue)
        {
            throw new InvalidOperationException(
                $"Test case '{testCase.Id}' must have a schema defined. " +
                "Schema should be provided in each test case.");
        }

        var schema = testCase.Schema.Value;
        
        var agent = await _agentFactory.CreateDataMappingAgentAsync(new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                schema: schema,
                schemaName: "DynamicOutput",
                schemaDescription: "Mapped JSON output"
            )
        });

        // Temporarily override instructions with custom prompt
        // Note: This requires modifying the agent factory or using reflection
        // For now, we'll create a new agent with custom instructions
        // This is a simplified approach - in production, we'd need a better way
        
        // Use the provided prompt, interpolating {input} if present, or appending input otherwise
        var userMessage = prompt.Contains("{input}")
            ? prompt.Replace("{input}", testCase.Input)
            : $"{prompt}\nInput:\n{testCase.Input}";
        var response = await agent.RunAsync(userMessage, cancellationToken: cancellationToken);

        return response?.Text ?? string.Empty;
    }

    private async Task<Dictionary<string, double>> EvaluateResponseAsync(
        string input,
        string response,
        Dictionary<string, IEvaluator> evaluators,
        CancellationToken cancellationToken)
    {
        var scores = new Dictionary<string, double>();
        var chatConfig = new ChatConfiguration(_judgeClient);
        var userMessage = new ChatMessage(ChatRole.User, input);
        var assistantMessage = new ChatMessage(ChatRole.Assistant, response);

        foreach (var (metricName, evaluator) in evaluators)
        {
            try
            {
                var result = await evaluator.EvaluateAsync(
                    userMessage,
                    assistantMessage,
                    chatConfig,
                    cancellationToken: cancellationToken);

                if (result.Metrics.Any())
                {
                    var metricKey = result.Metrics.Keys.First();
                    var metric = result.Metrics[metricKey];

                    if (metric is NumericMetric numericMetric && numericMetric.Value != null)
                    {
                        scores[metricName] = (double)numericMetric.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evaluate {Metric}", metricName);
            }
        }

        return scores;
    }

    private static double CalculateStdDev(List<double> values, double average)
    {
        if (values.Count < 2) return 0;
        
        var sumOfSquares = values.Sum(v => Math.Pow(v - average, 2));
        return Math.Sqrt(sumOfSquares / values.Count);
    }

    private static double CalculateCompositeScore(
        Dictionary<string, double> averageScores,
        Dictionary<string, double> weights)
    {
        double weightedSum = 0;
        double totalWeight = 0;

        foreach (var (metricName, score) in averageScores)
        {
            var weight = weights.GetValueOrDefault(metricName, 1.0);
            weightedSum += score * weight;
            totalWeight += weight;
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 0;
    }

    private static string GenerateSummary(PromptComparison comparison)
    {
        var parts = new List<string>();

        if (comparison.IsImprovement)
        {
            parts.Add($"✓ IMPROVEMENT: +{comparison.DeltaCompositeScore:F2} composite score");
        }
        else
        {
            parts.Add($"✗ NO IMPROVEMENT: {comparison.DeltaCompositeScore:+F2;-F2} composite score");
        }

        if (comparison.ImprovedMetrics.Any())
        {
            parts.Add($"Improved: {string.Join(", ", comparison.ImprovedMetrics)}");
        }

        if (comparison.RegressedMetrics.Any())
        {
            parts.Add($"Regressed: {string.Join(", ", comparison.RegressedMetrics)}");
        }

        return string.Join(" | ", parts);
    }
}
