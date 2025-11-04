using AgenticStructuredOutput.Extensions;
using AgenticStructuredOutput.Optimization;
using AgenticStructuredOutput.Optimization.Core;
using AgenticStructuredOutput.Optimization.Evaluation;
using AgenticStructuredOutput.Optimization.Models;
using AgenticStructuredOutput.Optimization.Strategies;
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
            // Parse command line arguments
            var schemaPath = GetArgument(args, "--schema");
            var promptPath = GetArgument(args, "--prompt");
            var testCasesPath = GetArgument(args, "--test-cases");
            var maxIterations = GetIntArgument(args, "--max-iterations", 5);

            // Display configuration
            Console.WriteLine("Configuration:");
            Console.WriteLine($"  Schema: {schemaPath ?? "(solution root)/schema.json"}");
            Console.WriteLine($"  Prompt: {promptPath ?? "(solution root)/agent-instructions.md"}");
            Console.WriteLine($"  Test Cases: {testCasesPath ?? "(hardcoded demo cases)"}");
            Console.WriteLine($"  Max Iterations: {maxIterations}");
            Console.WriteLine();

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
                        var agentFactory = sp.GetRequiredService<Services.IAgentFactory>();
                        var judgeClient = new AzureInferenceChatClientBuilder()
                            .UseGitHubModelsEndpoint()
                            .WithEnvironmentApiKey()
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
                })
                .Build();

            // Run optimization
            using var scope = host.Services.CreateScope();
            var optimizer = scope.ServiceProvider.GetRequiredService<IPromptOptimizer>();

            // Load resources from solution root
            Console.WriteLine("Loading resources from solution root...");
            var schema = ResourceLoader.LoadSchema(schemaPath);
            var baselinePrompt = ResourceLoader.LoadPrompt(promptPath);
            var testCases = testCasesPath != null 
                ? LoadTestCasesFromFile(testCasesPath, schema)
                : CreateDemoTestCases(schema);

            Console.WriteLine($"Baseline Prompt Length: {baselinePrompt.Length} characters");
            Console.WriteLine($"Loaded {testCases.Count} test cases");
            Console.WriteLine();

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

            Console.WriteLine("Starting optimization...");
            Console.WriteLine($"Enabled Strategies: {string.Join(", ", config.EnabledStrategies)}");
            Console.WriteLine();

            // Run optimization
            var result = await optimizer.OptimizeAsync(
                baselinePrompt,
                testCases,
                config,
                CancellationToken.None);

            // Display results
            DisplayResults(result);

            // Save optimized prompt back to solution root
            var savedPath = promptPath ?? ResourceLoader.GetResourcePath("agent-instructions.md");
            ResourceLoader.SavePrompt(result.BestPrompt, savedPath);
            Console.WriteLine($"✓ Optimized prompt saved to: {savedPath}");
            Console.WriteLine("  (Source control will track this change)");
            Console.WriteLine();

            return result.DidImprove ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            if (args.Contains("--verbose"))
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    private static void DisplayResults(OptimizationResult result)
    {
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
    }

    private static List<EvalTestCase> CreateDemoTestCases(JsonElement schema)
    {
        // Create demo test cases with the loaded schema
        return new List<EvalTestCase>
        {
            new EvalTestCase
            {
                Id = "test-001",
                EvaluationType = "Relevance",
                TestScenario = "Simple name mapping",
                Input = "{\"first_name\": \"John\", \"last_name\": \"Doe\", \"email_address\": \"john@example.com\"}",
                ExpectedOutput = "{\"firstName\": \"John\", \"lastName\": \"Doe\", \"email\": \"john@example.com\"}",
                Schema = schema
            },
            new EvalTestCase
            {
                Id = "test-002",
                EvaluationType = "Correctness",
                TestScenario = "Nested structure",
                Input = "{\"name\": {\"first\": \"Jane\", \"last\": \"Smith\"}, \"contact\": {\"email\": \"jane@example.com\"}}",
                ExpectedOutput = "{\"firstName\": \"Jane\", \"lastName\": \"Smith\", \"email\": \"jane@example.com\"}",
                Schema = schema
            },
            new EvalTestCase
            {
                Id = "test-003",
                EvaluationType = "Completeness",
                TestScenario = "All fields present",
                Input = "{\"first_name\": \"Bob\", \"last_name\": \"Johnson\", \"email_address\": \"bob@example.com\"}",
                ExpectedOutput = "{\"firstName\": \"Bob\", \"lastName\": \"Johnson\", \"email\": \"bob@example.com\"}",
                Schema = schema
            }
        };
    }

    private static List<EvalTestCase> LoadTestCasesFromFile(string filePath, JsonElement defaultSchema)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Test cases file not found: {filePath}");
        }

        var testCases = new List<EvalTestCase>();
        var lines = File.ReadAllLines(filePath);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var testCase = JsonSerializer.Deserialize<EvalTestCase>(line);
            if (testCase != null)
            {
                // If test case doesn't have schema, use the default one
                if (!testCase.Schema.HasValue)
                {
                    testCase.Schema = defaultSchema;
                }
                testCases.Add(testCase);
            }
        }

        return testCases;
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
