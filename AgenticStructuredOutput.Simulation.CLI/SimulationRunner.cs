using AgenticStructuredOutput.Optimization;
using AgenticStructuredOutput.Simulation.Core;
using AgenticStructuredOutput.Simulation.Models;
using Microsoft.Extensions.Logging;

namespace AgenticStructuredOutput.Simulation.CLI;

public sealed class SimulationRunner(
    IEvalGenerator generator,
    IEvalPersistence persistence,
    ILogger<SimulationRunner> logger)
{
    private static readonly IReadOnlyList<string> DefaultEvaluationTypes
        = new SimulationConfig().EvaluationTypes;

    public async Task<int> RunAsync(string[] args)
    {
        var schemaPath = GetArgument(args, "--schema");
        var promptPath = GetArgument(args, "--prompt");
        var outputPath = GetArgument(args, "--output");
        var testCaseCount = GetIntArgument(args, "--count", 10);
        var diversity = GetDoubleArgument(args, "--diversity", 0.7);
        var temperature = GetDoubleArgument(args, "--temperature", 0.8);
        var includeEdgeCases = !HasFlag(args, "--no-edge-cases");
        var appendMode = HasFlag(args, "--append");
        var evaluationTypes = ParseEvaluationTypes(GetArgument(args, "--types"));

        var resolvedOutputPath = ResolveOutputPath(outputPath);

        logger.LogInformation("Configuration:");
        logger.LogInformation("  Schema: {SchemaPath}", schemaPath ?? "(solution root)/schema.json");
        logger.LogInformation("  Prompt: {PromptPath}", promptPath ?? "(solution root)/agent-instructions.md");
        logger.LogInformation("  Output: {OutputPath}", resolvedOutputPath);
        logger.LogInformation("  Count: {Count}", testCaseCount);
        logger.LogInformation("  Diversity: {Diversity:F2}", diversity);
        logger.LogInformation("  Temperature: {Temperature:F2}", temperature);
        logger.LogInformation("  Include Edge Cases: {IncludeEdgeCases}", includeEdgeCases);
        logger.LogInformation("  Evaluation Types: {EvaluationTypes}", string.Join(", ", evaluationTypes));
        logger.LogInformation("  Append Mode: {AppendMode}", appendMode);
        logger.LogInformation(string.Empty);

        var schema = ResourceLoader.LoadSchema(schemaPath);
        var prompt = ResourceLoader.LoadPrompt(promptPath);

        var config = new SimulationConfig
        {
            TestCaseCount = testCaseCount,
            DiversityFactor = diversity,
            Temperature = (float)temperature,
            IncludeEdgeCases = includeEdgeCases,
            EvaluationTypes = evaluationTypes.ToList()
        };

        logger.LogInformation("Starting simulation...");
        var result = await generator.GenerateTestCasesAsync(prompt, schema, config);
        logger.LogInformation("Generated {Count} test cases in {Duration:F1}s", result.SuccessCount, result.Duration.TotalSeconds);

        var casesToPersist = result.TestCases;
        if (appendMode && File.Exists(resolvedOutputPath))
        {
            var existing = await persistence.LoadTestCasesAsync(resolvedOutputPath);
            casesToPersist = [.. existing, .. result.TestCases];
            logger.LogInformation("Append mode enabled: merging {ExistingCount} existing cases", existing.Count);
        }

        await persistence.SaveTestCasesAsync(casesToPersist, resolvedOutputPath);
        logger.LogInformation("✓ Test cases saved to: {Path}", resolvedOutputPath);
        logger.LogInformation("  Review with: git diff {Relative}", GetRelativePathFromRoot(resolvedOutputPath));

        return result.SuccessCount > 0 ? 0 : 1;
    }

    private static string ResolveOutputPath(string? providedPath)
    {
        if (!string.IsNullOrWhiteSpace(providedPath))
        {
            return Path.GetFullPath(providedPath);
        }

        var solutionRoot = ResourceLoader.GetSolutionRoot();
        return Path.Combine(solutionRoot, "generated-test-cases.jsonl");
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
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static double GetDoubleArgument(string[] args, string flag, double defaultValue)
    {
        var value = GetArgument(args, flag);
        return double.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static bool HasFlag(string[] args, string flag)
    {
        return Array.IndexOf(args, flag) >= 0;
    }

    private static IReadOnlyList<string> ParseEvaluationTypes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DefaultEvaluationTypes;
        }

        var parts = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return parts.Count > 0 ? parts : DefaultEvaluationTypes;
    }

    private static string GetRelativePathFromRoot(string absolutePath)
    {
        try
        {
            var root = ResourceLoader.GetSolutionRoot();
            var relative = Path.GetRelativePath(root, absolutePath);
            return relative.Replace('\\', '/');
        }
        catch
        {
            return absolutePath;
        }
    }
}
