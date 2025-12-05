using AgenticStructuredOutput.Optimization.Models;
using AgenticStructuredOutput.Services;
using AgenticStructuredOutput.Simulation.Core;
using AgenticStructuredOutput.Simulation.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AgenticStructuredOutput.Simulation;

/// <summary>
/// Generates evaluation test cases using an LLM agent based on a prompt and schema.
/// </summary>
public class LlmEvalGenerator : IEvalGenerator
{
    private readonly IAgentFactory _agentFactory;
    private readonly ILogger<LlmEvalGenerator> _logger;

    public LlmEvalGenerator(
        IAgentFactory agentFactory,
        ILogger<LlmEvalGenerator> logger)
    {
        _agentFactory = agentFactory;
        _logger = logger;
    }

    public async Task<SimulationResult> GenerateTestCasesAsync(
        string prompt,
        JsonElement schema,
        SimulationConfig config,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTime.UtcNow;
        
        _logger.LogInformation("Starting test case generation: {Count} cases requested", config.TestCaseCount);

        var result = new SimulationResult
        {
            Config = config,
            StartTime = startTime,
            SourcePrompt = prompt
        };

        // Generate test cases in batches for efficiency
        var batchSize = Math.Min(5, config.TestCaseCount);
        var batchCount = (int)Math.Ceiling((double)config.TestCaseCount / batchSize);

        for (int batchIndex = 0; batchIndex < batchCount; batchIndex++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var remainingCount = config.TestCaseCount - result.TestCases.Count;
            var currentBatchSize = Math.Min(batchSize, remainingCount);

            _logger.LogInformation("Generating batch {BatchIndex}/{BatchCount} ({BatchSize} cases)", 
                batchIndex + 1, batchCount, currentBatchSize);

            var batchTestCases = await GenerateBatchAsync(
                prompt,
                schema,
                config,
                currentBatchSize,
                batchIndex,
                cancellationToken);

            result.TestCases.AddRange(batchTestCases);
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        result.EndTime = DateTime.UtcNow;

        _logger.LogInformation(
            "Test case generation complete: {Count} cases generated in {Duration:F1}s",
            result.TestCases.Count,
            result.Duration.TotalSeconds);

        return result;
    }

    private async Task<List<EvalTestCase>> GenerateBatchAsync(
        string prompt,
        JsonElement schema,
        SimulationConfig config,
        int count,
        int batchIndex,
        CancellationToken cancellationToken)
    {
        var generationPrompt = BuildGenerationPrompt(prompt, schema, config, count, batchIndex);

        try
        {
            // Create a simple agent for generation (no specific schema needed)
            var agent = await _agentFactory.CreateDataMappingAgentAsync(new Microsoft.Extensions.AI.ChatOptions());
            
            var response = await agent.RunAsync(generationPrompt, cancellationToken: cancellationToken);
            var responseText = response?.Text ?? string.Empty;

            return ParseTestCases(responseText, schema, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate batch {BatchIndex}", batchIndex);
            return new List<EvalTestCase>();
        }
    }

    private string BuildGenerationPrompt(
        string prompt,
        JsonElement schema,
        SimulationConfig config,
        int count,
        int batchIndex)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"Generate {count} diverse test case inputs for evaluating the following prompt:");
        sb.AppendLine();
        sb.AppendLine("**Target Prompt:**");
        sb.AppendLine("```");
        sb.AppendLine(prompt);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("**Output Schema:**");
        sb.AppendLine("```json");
        sb.AppendLine(JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true }));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine($"Generate test cases that evaluate: {string.Join(", ", config.EvaluationTypes)}");
        sb.AppendLine();
        sb.AppendLine("Requirements:");
        sb.AppendLine($"- Generate {count} distinct test case inputs");
        sb.AppendLine("- Each input should be a JSON object that could be mapped to the schema");
        sb.AppendLine("- Vary the field names, nesting levels, and data formats");
        sb.AppendLine("- Include both simple and complex nested structures");
        
        if (config.IncludeEdgeCases && batchIndex == 0)
        {
            sb.AppendLine("- Include edge cases: missing fields, unusual naming, deep nesting");
        }
        
        sb.AppendLine($"- Diversity factor: {config.DiversityFactor:F1} (higher = more creative variations)");
        sb.AppendLine();
        sb.AppendLine("Output format: One JSON object per line (JSONL format):");
        sb.AppendLine("{\"evaluationType\": \"Relevance\", \"testScenario\": \"Brief description\", \"input\": \"{...JSON...}\"}");
        sb.AppendLine();
        sb.AppendLine("Generate realistic, varied test inputs now:");

        return sb.ToString();
    }

    private List<EvalTestCase> ParseTestCases(
        string responseText,
        JsonElement schema,
        SimulationConfig config)
    {
        var testCases = new List<EvalTestCase>();
        var lines = responseText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Skip lines that are clearly not JSON
            if (!trimmedLine.StartsWith("{")) continue;

            try
            {
                // Parse the line as a partial test case (just the metadata)
                using var doc = JsonDocument.Parse(trimmedLine);
                var root = doc.RootElement;

                var testCase = new EvalTestCase
                {
                    Id = $"sim-{Guid.NewGuid().ToString()[..8]}",
                    EvaluationType = root.TryGetProperty("evaluationType", out var evalType) 
                        ? evalType.GetString() ?? "General" 
                        : "General",
                    TestScenario = root.TryGetProperty("testScenario", out var scenario) 
                        ? scenario.GetString() ?? "" 
                        : "",
                    Input = root.TryGetProperty("input", out var input) 
                        ? input.GetRawText() 
                        : trimmedLine,
                    Schema = schema
                };

                // Optionally set expected output if generated
                if (config.GenerateExpectedOutputs && root.TryGetProperty("expectedOutput", out var expectedOutput))
                {
                    testCase.ExpectedOutput = expectedOutput.GetRawText();
                }

                testCases.Add(testCase);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse generated test case line: {Line}", trimmedLine);
            }
        }

        _logger.LogInformation("Parsed {Count} test cases from response", testCases.Count);
        return testCases;
    }
}
