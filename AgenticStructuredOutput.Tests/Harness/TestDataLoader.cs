using System.Text.Json.Nodes;

namespace AgenticStructuredOutput.Tests.Harness;

/// <summary>
/// Loads test data from JSONL files with type-safe parsing and validation.
/// Encapsulates common patterns for both integration and evaluation test case loading.
/// </summary>
public static class TestDataLoader
{
    /// <summary>
    /// Loads integration test cases from a JSONL file.
    /// Each line should have: testScenario, input, expectedOutput
    /// </summary>
    public static IEnumerable<TestCaseData> LoadIntegrationTestCases(string filePath)
    {
        if (!File.Exists(filePath))
            throw new InvalidOperationException($"Test cases file not found: {filePath}");

        foreach (var testCase in LoadJsonLFile(filePath))
        {
            var testScenario = testCase["testScenario"]?.GetValue<string>();
            var input = testCase["input"]?.GetValue<string>();
            var expectedOutput = testCase["expectedOutput"]?.GetValue<string>();

            if (testScenario == null || input == null || expectedOutput == null)
            {
                throw new InvalidOperationException(
                    $"Integration test case missing required fields: testScenario, input, expectedOutput. Got: {testCase}");
            }

            yield return new TestCaseData(testScenario, input, expectedOutput)
                .SetName($"Agent_ShouldMapInputSuccessfully_{SanitizeTestName(testScenario)}");
        }
    }

    /// <summary>
    /// Loads evaluation test cases from a JSONL file.
    /// Each line should have: evaluationType, testScenario, input
    /// </summary>
    public static IEnumerable<TestCaseData> LoadEvaluationTestCases(string filePath)
    {
        if (!File.Exists(filePath))
            throw new InvalidOperationException($"Test cases file not found: {filePath}");

        foreach (var testCase in LoadJsonLFile(filePath))
        {
            var evaluationType = testCase["evaluationType"]?.GetValue<string>();
            var testScenario = testCase["testScenario"]?.GetValue<string>();
            var input = testCase["input"]?.GetValue<string>();

            if (evaluationType == null || testScenario == null || input == null)
            {
                throw new InvalidOperationException(
                    $"Evaluation test case missing required fields: evaluationType, testScenario, input. Got: {testCase}");
            }

            yield return new TestCaseData(evaluationType, testScenario, input)
                .SetName($"Evaluation_{SanitizeTestName($"{evaluationType}_{testScenario}")}");
        }
    }

    /// <summary>
    /// Internal: Parse JSONL file and yield non-empty JSON objects.
    /// </summary>
    private static IEnumerable<JsonObject> LoadJsonLFile(string filePath)
    {
        foreach (var line in File.ReadLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var obj = JsonNode.Parse(line)?.AsObject();
            if (obj == null)
                throw new InvalidOperationException($"Failed to parse JSON line: {line}");

            yield return obj;
        }
    }

    /// <summary>
    /// Sanitize test scenario names for use in NUnit test names.
    /// Replaces spaces, dashes, and special chars with underscores.
    /// </summary>
    private static string SanitizeTestName(string scenario)
    {
        return System.Text.RegularExpressions.Regex.Replace(scenario, @"[^\w]", "_");
    }
}
