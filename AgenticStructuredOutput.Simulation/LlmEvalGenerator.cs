using AgenticStructuredOutput.Optimization.Models;
using AgenticStructuredOutput.Services;
using AgenticStructuredOutput.Simulation.Core;
using AgenticStructuredOutput.Simulation.Models;
using Json.Schema;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgenticStructuredOutput.Simulation;

/// <summary>
/// Generates evaluation test cases using an LLM agent based on a prompt and schema.
/// </summary>
public class LlmEvalGenerator(
    IAgentFactory agentFactory,
    ILogger<LlmEvalGenerator> logger) : IEvalGenerator
{
    private readonly IAgentFactory _agentFactory = agentFactory;
    private readonly ILogger<LlmEvalGenerator> _logger = logger;

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
            var agent = await _agentFactory.CreateDataMappingAgentAsync(new ChatOptions());
            
            var response = await agent.RunAsync(generationPrompt, cancellationToken: cancellationToken);
            var responseText = response?.Text ?? string.Empty;

            var parsedCases = ParseTestCases(responseText, schema);

            if (parsedCases.Count > 0)
            {
                await PopulateExpectedOutputsAsync(parsedCases, schema, cancellationToken);
            }

            return parsedCases;
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
        JsonElement schema)
    {
        var testCases = new List<EvalTestCase>();
        var lines = responseText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            if (!line.StartsWith("{", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                if (!TryNormalizeInputPayload(root, out var normalizedInput))
                {
                    _logger.LogWarning("Skipping generated test case without usable 'input': {Line}", line);
                    continue;
                }

                var testCase = new EvalTestCase
                {
                    Id = $"sim-{Guid.NewGuid().ToString()[..8]}",
                    EvaluationType = root.TryGetProperty("evaluationType", out var evalType)
                        ? evalType.GetString() ?? "General"
                        : "General",
                    TestScenario = root.TryGetProperty("testScenario", out var scenario)
                        ? scenario.GetString() ?? string.Empty
                        : string.Empty,
                    Input = normalizedInput,
                    Schema = schema
                };

                testCases.Add(testCase);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse generated test case line: {Line}", line);
            }
        }

        _logger.LogInformation("Parsed {Count} test cases from response", testCases.Count);
        return testCases;
    }

    private async Task PopulateExpectedOutputsAsync(
        List<EvalTestCase> testCases,
        JsonElement defaultSchema,
        CancellationToken cancellationToken)
    {
        var schemaContexts = new Dictionary<string, SchemaExecutionContext>(StringComparer.Ordinal);

        foreach (var testCase in testCases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var schemaElement = testCase.Schema ?? defaultSchema;
            var schemaKey = schemaElement.GetRawText();

            if (!schemaContexts.TryGetValue(schemaKey, out var context))
            {
                var compiledSchema = JsonSchema.FromText(schemaKey);
                var agent = await _agentFactory.CreateDataMappingAgentAsync(new ChatOptions
                {
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(
                        schema: schemaElement,
                        schemaName: "DynamicOutput",
                        schemaDescription: "Intelligently mapped JSON output conforming to the provided schema"
                    )
                });

                context = new SchemaExecutionContext(compiledSchema, agent);
                schemaContexts[schemaKey] = context;
            }

            await GenerateExpectedOutputForCaseAsync(testCase, context, cancellationToken);
        }
    }

    private async Task GenerateExpectedOutputForCaseAsync(
        EvalTestCase testCase,
        SchemaExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(testCase.Input))
        {
            _logger.LogWarning("Skipping expected output generation for {TestCaseId}: input is empty", testCase.Id);
            return;
        }

        try
        {
            var prompt = BuildMappingPrompt(testCase.Input);
            var response = await context.Agent.RunAsync(prompt, cancellationToken: cancellationToken);
            var expectedOutput = response?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(expectedOutput))
            {
                _logger.LogWarning("Agent returned empty expected output for {TestCaseId}", testCase.Id);
                return;
            }

            if (!TryValidateAgainstSchema(context.Validator, expectedOutput, out var validationError))
            {
                _logger.LogWarning(
                    "Generated expected output failed schema validation for {TestCaseId}: {Error}",
                    testCase.Id,
                    validationError);
                return;
            }

            testCase.ExpectedOutput = expectedOutput;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate expected output for test case {TestCaseId}", testCase.Id);
        }
    }

    private static string BuildMappingPrompt(string normalizedInput)
    {
        return $"Map the following input JSON to the target schema and respond with valid JSON only.\nInput:\n{normalizedInput}";
    }

    private static bool TryNormalizeInputPayload(JsonElement root, out string normalized)
    {
        normalized = string.Empty;
        if (!root.TryGetProperty("input", out var inputElement))
        {
            return false;
        }

        return TryNormalizeJsonPayload(inputElement, out normalized);
    }

    private static bool TryNormalizeJsonPayload(JsonElement element, out string normalized)
    {
        normalized = string.Empty;
        if (element.ValueKind == JsonValueKind.String)
        {
            return TryNormalizeJsonPayload(element.GetString() ?? string.Empty, out normalized);
        }

        return TryNormalizeJsonPayload(element.GetRawText(), out normalized);
    }

    private static bool TryNormalizeJsonPayload(string raw, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (TryParseJson(raw, out normalized))
        {
            return true;
        }

        try
        {
            var decoded = JsonSerializer.Deserialize<string>(raw);
            return decoded is not null && TryParseJson(decoded, out normalized);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseJson(string text, out string normalized)
    {
        normalized = string.Empty;

        try
        {
            var node = JsonNode.Parse(text);
            if (node is null)
            {
                return false;
            }

            normalized = node.ToJsonString();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryValidateAgainstSchema(JsonSchema schema, string jsonText, out string? error)
    {
        try
        {
            var node = JsonNode.Parse(jsonText);
            if (node is null)
            {
                error = "Agent response was not valid JSON.";
                return false;
            }

            var evaluation = schema.Evaluate(node);
            if (evaluation.IsValid)
            {
                error = null;
                return true;
            }

            if (evaluation.Errors is { Count: > 0 })
            {
                var builder = new StringBuilder();
                foreach (var (keyword, message) in evaluation.Errors)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append("; ");
                    }
                    builder.Append(keyword);
                    builder.Append(':');
                    builder.Append(' ');
                    builder.Append(message);
                }
                error = builder.ToString();
            }
            else
            {
                error = "Schema validation failed.";
            }

            return false;
        }
        catch (JsonException)
        {
            error = "Agent response was not valid JSON.";
            return false;
        }
    }

    private sealed record SchemaExecutionContext(JsonSchema Validator, AIAgent Agent);
}
