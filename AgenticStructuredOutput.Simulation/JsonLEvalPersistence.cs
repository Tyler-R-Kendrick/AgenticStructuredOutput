using AgenticStructuredOutput.Optimization.Models;
using AgenticStructuredOutput.Simulation.Core;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AgenticStructuredOutput.Simulation;

/// <summary>
/// Persists evaluation test cases to/from JSONL files.
/// </summary>
public class JsonLEvalPersistence : IEvalPersistence
{
    private readonly ILogger<JsonLEvalPersistence> _logger;
    private readonly JsonSerializerOptions _serializerOptions;

    public JsonLEvalPersistence(ILogger<JsonLEvalPersistence> logger)
    {
        _logger = logger;
        _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task SaveTestCasesAsync(
        IEnumerable<EvalTestCase> testCases,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var testCaseList = testCases.ToList();
        _logger.LogInformation("Saving {Count} test cases to {FilePath}", testCaseList.Count, filePath);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(filePath, false);
        
        foreach (var testCase in testCaseList)
        {
            var json = JsonSerializer.Serialize(testCase, _serializerOptions);
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
        }

        _logger.LogInformation("Successfully saved {Count} test cases to {FilePath}", testCaseList.Count, filePath);
    }

    public async Task<List<EvalTestCase>> LoadTestCasesAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Test cases file not found: {filePath}");
        }

        _logger.LogInformation("Loading test cases from {FilePath}", filePath);
        
        var testCases = new List<EvalTestCase>();
        using var reader = new StreamReader(filePath);
        
        string? line;
        int lineNumber = 0;
        
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var testCase = JsonSerializer.Deserialize<EvalTestCase>(line, _serializerOptions);
                if (testCase != null)
                {
                    testCases.Add(testCase);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse test case at line {LineNumber}", lineNumber);
            }
        }

        _logger.LogInformation("Loaded {Count} test cases from {FilePath}", testCases.Count, filePath);
        return testCases;
    }
}
