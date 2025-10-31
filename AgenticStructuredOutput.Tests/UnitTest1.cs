using System.Diagnostics;
using System.Text.Json;

namespace AgenticStructuredOutput.Tests;

/// <summary>
/// Evaluation tests for the Agentic Structured Output using Microsoft Agent Framework.
/// These tests demonstrate validity across common evaluation criteria for agents:
/// - Correctness: Does it produce accurate outputs with fuzzy matching?
/// - Robustness: Does it handle edge cases and errors gracefully?
/// - Agent Intelligence: Does it use inference to map fields intelligently?
/// - Performance: Does it complete within reasonable time bounds?
/// </summary>
public class AgentEvaluationTests
{
    private readonly string _exePath;
    private readonly string _testDataPath;

    public AgentEvaluationTests()
    {
        // Get the path to the built executable using a more robust approach
        var testAssemblyPath = typeof(AgentEvaluationTests).Assembly.Location;
        var testDir = Path.GetDirectoryName(testAssemblyPath) ?? Directory.GetCurrentDirectory();
        
        // Navigate to the solution root and find the executable
        var currentDir = testDir;
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "AgenticStructuredOutput.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        
        if (currentDir == null)
        {
            throw new InvalidOperationException("Could not find solution root");
        }
        
        _exePath = Path.Combine(
            currentDir,
            "AgenticStructuredOutput",
            "bin",
            "Debug",
            "net9.0",
            "AgenticStructuredOutput.dll"
        );
        
        _testDataPath = Path.Combine(testDir, "TestData");
    }

    [Fact]
    public async Task Test_FuzzyMapping_PersonSchema_FieldNamesInfer()
    {
        // Arrange: Input has "fullName", "yearsOld", "emailAddress"
        // Schema expects "name", "age", "email"
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var inputPath = Path.Combine(_testDataPath, "person-input-fuzzy.json");

        // Skip test if no API key
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return; // Skip test
        }

        // Act
        var (exitCode, output, error) = await RunAgentAsync(schemaPath, inputPath);

        // Assert
        Assert.Equal(0, exitCode);
        // Agent should intelligently map fullName -> name, yearsOld -> age, emailAddress -> email
        Assert.Contains("John Doe", output);
        Assert.Contains("30", output);
        Assert.Contains("john.doe@example.com", output);
    }

    [Fact]
    public async Task Test_FuzzyMapping_NestedSchema_IntelligentInference()
    {
        // Arrange: Input has "person" but schema expects "user", etc.
        var schemaPath = Path.Combine(_testDataPath, "nested-schema.json");
        var inputPath = Path.Combine(_testDataPath, "nested-input-fuzzy.json");

        // Skip test if no API key
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return; // Skip test
        }

        // Act
        var (exitCode, output, error) = await RunAgentAsync(schemaPath, inputPath);

        // Assert
        Assert.Equal(0, exitCode);
        // Agent should map person->user, fullName->name, contactInfo->contact, labels->tags, etc.
        Assert.Contains("Jane Smith", output);
        Assert.Contains("jane@example.com", output);
        Assert.Contains("developer", output);
    }

    [Fact]
    public async Task Test_StringLiteralInput_ValidJson_ReturnsSuccess()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var jsonInput = "{\"firstName\":\"Alice\",\"ageInYears\":25}";

        // Skip test if no API key
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return; // Skip test
        }

        // Act
        var (exitCode, output, error) = await RunAgentAsync(schemaPath, jsonInput);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Alice", output);
    }

    [Fact]
    public async Task Test_InvalidJson_ReturnsError()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var invalidJson = "{invalid json}";

        // Act
        var (exitCode, output, error) = await RunAgentAsync(schemaPath, invalidJson);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid JSON", output);
    }

    [Fact]
    public async Task Test_NoArguments_ShowsUsage()
    {
        // Act
        var (exitCode, output, error) = await RunAgentAsync();

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("Usage:", output);
    }

    [Fact]
    public async Task Test_NoApiKey_ShowsError()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var inputPath = Path.Combine(_testDataPath, "person-input-fuzzy.json");
        
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

            // Act
            var (exitCode, output, error) = await RunAgentAsync(schemaPath, inputPath);

            // Assert
            Assert.Equal(1, exitCode);
            Assert.Contains("No API key found", output);
        }
        finally
        {
            // Restore original API key
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public async Task Test_Performance_CompleteWithinTimeLimit()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var inputPath = Path.Combine(_testDataPath, "person-input-fuzzy.json");

        // Skip test if no API key
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return; // Skip test
        }

        var stopwatch = Stopwatch.StartNew();

        // Act
        var (exitCode, output, error) = await RunAgentAsync(schemaPath, inputPath);
        stopwatch.Stop();

        // Assert - should complete within 30 seconds (agent API calls take longer)
        Assert.Equal(0, exitCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 30000, 
            $"Agent took too long: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Test_OutputIsValidJson()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var jsonInput = "{\"personName\":\"Bob\",\"yearsOfAge\":42}";

        // Skip test if no API key
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return; // Skip test
        }

        // Act
        var (exitCode, output, error) = await RunAgentAsync(schemaPath, jsonInput);

        // Assert
        Assert.Equal(0, exitCode);
        // Should be valid JSON
        var exception = Record.Exception(() => JsonDocument.Parse(output));
        Assert.Null(exception);
    }

    private async Task<(int exitCode, string output, string error)> RunAgentAsync(params string[] args)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { _exePath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            processStartInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(processStartInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start process");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        return (process.ExitCode, output, error);
    }
}

