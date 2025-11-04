using AgenticStructuredOutput.Extensions;
using AgenticStructuredOutput.Optimization.Core;
using AgenticStructuredOutput.Optimization.Evaluation;
using AgenticStructuredOutput.Optimization.Models;
using AgenticStructuredOutput.Optimization.Strategies;
using AgenticStructuredOutput.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AgenticStructuredOutput.Optimization.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   AgenticStructuredOutput - Prompt Optimization CLI      ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            // Build host
            var host = Host.CreateDefaultBuilder(args)
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
                        var agentFactory = sp.GetRequiredService<IAgentFactory>();
                        var judgeClient = new AzureInferenceChatClientBuilder()
                            .UseGitHubModelsEndpoint()
                            .WithEnvironmentApiKey()
                            .BuildIChatClient();
                        var defaultSchema = LoadDefaultSchema();
                        var logger = sp.GetRequiredService<ILogger<EvaluationAggregator>>();
                        
                        return new EvaluationAggregator(agentFactory, judgeClient, defaultSchema, logger);
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
                })
                .Build();

            // Run optimization
            using var scope = host.Services.CreateScope();
            var optimizer = scope.ServiceProvider.GetRequiredService<IPromptOptimizer>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            // Load baseline prompt
            var baselinePrompt = LoadBaselinePrompt();
            Console.WriteLine($"Baseline Prompt Length: {baselinePrompt.Length} characters");
            Console.WriteLine();

            // Load test cases
            var testCases = LoadTestCases();
            Console.WriteLine($"Loaded {testCases.Count} test cases");
            Console.WriteLine();

            // Configure optimization
            var config = new OptimizationConfig
            {
                MaxIterations = 5,  // Reduced for demo
                ImprovementThreshold = 0.05,
                EnabledStrategies = new List<string>
                {
                    "AddExamples",
                    "AddConstraints"
                },
                ParallelEvaluation = true,
                MaxParallelTasks = 2
            };

            Console.WriteLine("Starting optimization...");
            Console.WriteLine($"Max Iterations: {config.MaxIterations}");
            Console.WriteLine($"Enabled Strategies: {string.Join(", ", config.EnabledStrategies)}");
            Console.WriteLine();

            // Run optimization
            var result = await optimizer.OptimizeAsync(
                baselinePrompt,
                testCases,
                config,
                CancellationToken.None);

            // Display results
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("OPTIMIZATION RESULTS");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine($"Baseline Score:     {result.BaselineMetrics.CompositeScore:F2}");
            Console.WriteLine($"Best Score:         {result.BestMetrics.CompositeScore:F2}");
            Console.WriteLine($"Improvement:        {result.TotalImprovement:+F2;-F2}");
            Console.WriteLine($"Iterations:         {result.History.Count}");
            Console.WriteLine($"Duration:           {result.Duration.TotalSeconds:F1}s");
            Console.WriteLine($"Stopping Reason:    {result.StoppingReason}");
            Console.WriteLine();

            Console.WriteLine("Metric Breakdown:");
            foreach (var (metric, score) in result.BestMetrics.AverageScores.OrderBy(x => x.Key))
            {
                var baselineScore = result.BaselineMetrics.AverageScores.GetValueOrDefault(metric, 0);
                var delta = score - baselineScore;
                Console.WriteLine($"  {metric,-15} {score:F2} (Δ {delta:+F2;-F2})");
            }
            Console.WriteLine();

            Console.WriteLine("Iteration History:");
            foreach (var iteration in result.History)
            {
                var status = iteration.Accepted ? "✓" : "✗";
                Console.WriteLine($"  {iteration.IterationNumber,2}. {status} {iteration.StrategyUsed,-20} Score: {iteration.BestCandidateScore:F2} (Δ {iteration.Improvement:+F2;-F2})");
            }
            Console.WriteLine();

            // Save optimized prompt
            var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "optimized-prompt.md");
            await File.WriteAllTextAsync(outputPath, result.BestPrompt);
            Console.WriteLine($"Optimized prompt saved to: {outputPath}");
            Console.WriteLine();

            return result.DidImprove ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static JsonElement LoadDefaultSchema()
    {
        var assembly = typeof(AgentFactory).Assembly;
        var resourceName = "AgenticStructuredOutput.Resources.schema.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not find embedded resource: {resourceName}");
        using var reader = new StreamReader(stream);
        var schemaJson = reader.ReadToEnd();
        return JsonDocument.Parse(schemaJson).RootElement.Clone();
    }

    private static string LoadBaselinePrompt()
    {
        var assembly = typeof(AgentFactory).Assembly;
        var resourceName = "AgenticStructuredOutput.Resources.agent-instructions.md";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not find embedded resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static List<EvalTestCase> LoadTestCases()
    {
        // For demo purposes, create a few simple test cases
        // In production, you'd load from test-cases-eval.jsonl
        return new List<EvalTestCase>
        {
            new EvalTestCase
            {
                Id = "test-001",
                EvaluationType = "Relevance",
                TestScenario = "Simple name mapping",
                Input = "{\"first_name\": \"John\", \"last_name\": \"Doe\", \"email_address\": \"john@example.com\"}",
                ExpectedOutput = "{\"firstName\": \"John\", \"lastName\": \"Doe\", \"email\": \"john@example.com\"}"
            },
            new EvalTestCase
            {
                Id = "test-002",
                EvaluationType = "Correctness",
                TestScenario = "Nested structure",
                Input = "{\"name\": {\"first\": \"Jane\", \"last\": \"Smith\"}, \"contact\": {\"email\": \"jane@example.com\"}}",
                ExpectedOutput = "{\"firstName\": \"Jane\", \"lastName\": \"Smith\", \"email\": \"jane@example.com\"}"
            },
            new EvalTestCase
            {
                Id = "test-003",
                EvaluationType = "Completeness",
                TestScenario = "All fields present",
                Input = "{\"first_name\": \"Bob\", \"last_name\": \"Johnson\", \"email_address\": \"bob@example.com\"}",
                ExpectedOutput = "{\"firstName\": \"Bob\", \"lastName\": \"Johnson\", \"email\": \"bob@example.com\"}"
            }
        };
    }
}
