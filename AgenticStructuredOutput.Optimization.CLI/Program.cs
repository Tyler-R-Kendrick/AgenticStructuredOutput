using AgenticStructuredOutput.Extensions;
using AgenticStructuredOutput.Optimization;
using AgenticStructuredOutput.Optimization.Core;
using AgenticStructuredOutput.Optimization.Evaluation;
using AgenticStructuredOutput.Optimization.Models;
using AgenticStructuredOutput.Optimization.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgenticStructuredOutput.Optimization.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Build host first to get logger
        var host = CreateHost(args);
        
        using var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        // Display banner
        logger.LogInformation("╔═══════════════════════════════════════════════════════════╗");
        logger.LogInformation("║   AgenticStructuredOutput - Prompt Optimization CLI      ║");
        logger.LogInformation("╚═══════════════════════════════════════════════════════════╝");
        logger.LogInformation("");

        try
        {
            var runner = scope.ServiceProvider.GetRequiredService<OptimizationRunner>();
            return await runner.RunAsync(args);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Optimization failed: {Message}", ex.Message);
            return 1;
        }
    }

    private static IHost CreateHost(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                // Register agent services
                services.AddAgentServices();

                // Register optimization services
                services.AddSingleton<IEvaluationAggregator>(sp =>
                {
                    var agentFactory = sp.GetRequiredService<Services.IAgentFactory>();
                    var judgeClient = AzureInferenceChatClientBuilder
                        .CreateFromEnvironment()
                        .BuildIChatClient();
                    var logger = sp.GetRequiredService<ILogger<EvaluationAggregator>>();
                    
                    return new EvaluationAggregator(agentFactory, judgeClient, logger);
                });

                services.AddSingleton<IEnumerable<IPromptMutationStrategy>>(sp =>
                {
                    return new List<IPromptMutationStrategy>
                    {
                        new AddExamplesStrategy(),
                        new AddConstraintsStrategy(),
                        new RephraseInstructionsStrategy(),
                        new SimplifyLanguageStrategy()
                    };
                });

                services.AddSingleton<IPromptOptimizer, IterativeOptimizer>();
                services.AddSingleton<OptimizationRunner>();
            })
            .Build();
    }
}

/// <summary>
/// Handles the optimization workflow with proper logging abstraction.
/// </summary>
public class OptimizationRunner
{
    private readonly IPromptOptimizer _optimizer;
    private readonly ILogger<OptimizationRunner> _logger;

    public OptimizationRunner(
        IPromptOptimizer optimizer,
        ILogger<OptimizationRunner> logger)
    {
        _optimizer = optimizer;
        _logger = logger;
    }

    public async Task<int> RunAsync(string[] args)
    {
        // Parse command line arguments
        var schemaPath = GetArgument(args, "--schema");
        var promptPath = GetArgument(args, "--prompt");
        var testCasesPath = GetArgument(args, "--test-cases");
        var maxIterations = GetIntArgument(args, "--max-iterations", 5);

        // Display configuration
        _logger.LogInformation("Configuration:");
        _logger.LogInformation("  Schema: {SchemaPath}", schemaPath ?? "(solution root)/schema.json");
        _logger.LogInformation("  Prompt: {PromptPath}", promptPath ?? "(solution root)/agent-instructions.md");
        _logger.LogInformation("  Test Cases: {TestCasesPath}", testCasesPath ?? "(solution root)/test-cases-eval.jsonl");
        _logger.LogInformation("  Max Iterations: {MaxIterations}", maxIterations);
        _logger.LogInformation("");

        // Load resources from solution root
        _logger.LogInformation("Loading resources from solution root...");
        var schema = ResourceLoader.LoadSchema(schemaPath);
        var baselinePrompt = ResourceLoader.LoadPrompt(promptPath);
        var testCases = ResourceLoader.LoadTestCases(testCasesPath, schema);

        _logger.LogInformation("Baseline Prompt Length: {Length} characters", baselinePrompt.Length);
        _logger.LogInformation("Loaded {Count} test cases", testCases.Count);
        _logger.LogInformation("");

        // Configure optimization
        var config = new OptimizationConfig
        {
            MaxIterations = maxIterations,
            ImprovementThreshold = 0.05,
            EnabledStrategies = new List<string>
            {
                "AddExamples",
                "AddConstraints"
            },
            ParallelEvaluation = true,
            MaxParallelTasks = 2
        };

        _logger.LogInformation("Starting optimization...");
        _logger.LogInformation("Enabled Strategies: {Strategies}", string.Join(", ", config.EnabledStrategies));
        _logger.LogInformation("");

        // Run optimization
        var result = await _optimizer.OptimizeAsync(
            baselinePrompt,
            testCases,
            config,
            CancellationToken.None);

        // Display results
        DisplayResults(result);

        // Save optimized prompt back to solution root
        var savedPath = promptPath ?? ResourceLoader.GetResourcePath("agent-instructions.md");
        ResourceLoader.SavePrompt(result.BestPrompt, savedPath);
        _logger.LogInformation("✓ Optimized prompt saved to: {Path}", savedPath);
        _logger.LogInformation("  (Source control will track this change)");
        _logger.LogInformation("");

        return result.DidImprove ? 0 : 1;
    }

    private void DisplayResults(OptimizationResult result)
    {
        _logger.LogInformation("");
        _logger.LogInformation("═══════════════════════════════════════════════════════════");
        _logger.LogInformation("OPTIMIZATION RESULTS");
        _logger.LogInformation("═══════════════════════════════════════════════════════════");
        _logger.LogInformation("");
        _logger.LogInformation("Baseline Score:     {BaselineScore:F2}", result.BaselineMetrics.CompositeScore);
        _logger.LogInformation("Best Score:         {BestScore:F2}", result.BestMetrics.CompositeScore);
        _logger.LogInformation("Improvement:        {Improvement:+F2;-F2}", result.TotalImprovement);
        _logger.LogInformation("Iterations:         {Iterations}", result.History.Count);
        _logger.LogInformation("Duration:           {Duration:F1}s", result.Duration.TotalSeconds);
        _logger.LogInformation("Stopping Reason:    {StoppingReason}", result.StoppingReason);
        _logger.LogInformation("");

        _logger.LogInformation("Metric Breakdown:");
        foreach (var (metric, score) in result.BestMetrics.AverageScores.OrderBy(x => x.Key))
        {
            var baselineScore = result.BaselineMetrics.AverageScores.GetValueOrDefault(metric, 0);
            var delta = score - baselineScore;
            _logger.LogInformation("  {Metric,-15} {Score:F2} (Δ {Delta:+F2;-F2})", metric, score, delta);
        }
        _logger.LogInformation("");

        _logger.LogInformation("Iteration History:");
        foreach (var iteration in result.History)
        {
            var status = iteration.Accepted ? "✓" : "✗";
            _logger.LogInformation("  {IterationNumber,2}. {Status} {Strategy,-20} Score: {Score:F2} (Δ {Improvement:+F2;-F2})", 
                iteration.IterationNumber, status, iteration.StrategyUsed, iteration.BestCandidateScore, iteration.Improvement);
        }
        _logger.LogInformation("");
    }

    private static string? GetArgument(string[] args, string flag)
    {
        var index = Array.IndexOf(args, flag);
        if (index >= 0 && index + 1 < args.Length)
        {
            return args[index + 1];
        }
        return null;
    }

    private static int GetIntArgument(string[] args, string flag, int defaultValue)
    {
        var value = GetArgument(args, flag);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }
}
