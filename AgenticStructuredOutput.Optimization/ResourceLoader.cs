using System.Text.Json;
using AgenticStructuredOutput.Optimization.Models;

namespace AgenticStructuredOutput.Optimization;

/// <summary>
/// Helper class for loading resources from the solution root directory.
/// Provides centralized access to shared resources like schema.json and agent-instructions.md.
/// </summary>
public static class ResourceLoader
{
    /// <summary>
    /// Gets the solution root directory by walking up from the executing assembly location.
    /// </summary>
    public static string GetSolutionRoot()
    {
        var assemblyLocation = Path.GetDirectoryName(typeof(ResourceLoader).Assembly.Location)
            ?? throw new InvalidOperationException("Could not determine assembly location");

        var current = new DirectoryInfo(assemblyLocation);
        
        // Walk up until we find the solution file or reach the root
        while (current != null)
        {
            // Check for solution file (.slnx or .sln)
            if (current.GetFiles("*.slnx").Any() || current.GetFiles("*.sln").Any())
            {
                return current.FullName;
            }
            
            current = current.Parent;
        }
        
        throw new InvalidOperationException(
            "Could not find solution root. Expected to find .slnx or .sln file in parent directories.");
    }

    /// <summary>
    /// Loads the default schema from the solution root.
    /// </summary>
    public static JsonElement LoadSchema(string? schemaPath = null)
    {
        var path = schemaPath ?? Path.Combine(GetSolutionRoot(), "schema.json");
        
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Schema file not found at: {path}. " +
                "Expected schema.json in solution root.", 
                path);
        }

        var schemaJson = File.ReadAllText(path);
        return JsonDocument.Parse(schemaJson).RootElement.Clone();
    }

    /// <summary>
    /// Loads the agent instructions/prompt from the solution root.
    /// </summary>
    public static string LoadPrompt(string? promptPath = null)
    {
        var path = promptPath ?? Path.Combine(GetSolutionRoot(), "agent-instructions.md");
        
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Prompt file not found at: {path}. " +
                "Expected agent-instructions.md in solution root.", 
                path);
        }

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Saves the optimized prompt back to the solution root, overwriting the existing file.
    /// This allows source control to track changes.
    /// </summary>
    public static void SavePrompt(string prompt, string? promptPath = null)
    {
        var path = promptPath ?? Path.Combine(GetSolutionRoot(), "agent-instructions.md");
        File.WriteAllText(path, prompt);
    }

    /// <summary>
    /// Gets the full path to a resource in the solution root.
    /// </summary>
    public static string GetResourcePath(string fileName)
    {
        return Path.Combine(GetSolutionRoot(), fileName);
    }

    /// <summary>
    /// Loads test cases from a JSONL file.
    /// Each line in the file should be a JSON object representing an EvalTestCase.
    /// If test cases don't have a schema, the provided default schema is applied.
    /// </summary>
    /// <param name="testCasesPath">Path to JSONL file. If null, loads from solution root/test-cases-eval.jsonl</param>
    /// <param name="defaultSchema">Default schema to apply to test cases that don't specify one</param>
    /// <returns>List of evaluation test cases</returns>
    public static List<EvalTestCase> LoadTestCases(string? testCasesPath = null, JsonElement? defaultSchema = null)
    {
        var path = testCasesPath ?? Path.Combine(GetSolutionRoot(), "test-cases-eval.jsonl");
        
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Test cases file not found at: {path}. " +
                "Expected test-cases-eval.jsonl in solution root or provide --test-cases argument.", 
                path);
        }

        var testCases = new List<EvalTestCase>();
        var lines = File.ReadAllLines(path);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var testCase = JsonSerializer.Deserialize<EvalTestCase>(line);
            if (testCase != null)
            {
                // If test case doesn't have schema and default is provided, use the default
                if (!testCase.Schema.HasValue && defaultSchema.HasValue)
                {
                    testCase.Schema = defaultSchema.Value;
                }
                testCases.Add(testCase);
            }
        }

        if (testCases.Count == 0)
        {
            throw new InvalidOperationException(
                $"No valid test cases found in file: {path}. " +
                "Ensure file contains valid JSONL format with EvalTestCase objects.");
        }

        return testCases;
    }
}
