using System.Diagnostics;

namespace AgenticStructuredOutput.Tests;

/// <summary>
/// Evaluation tests for the Agentic Structured Output agent.
/// These tests demonstrate validity across common evaluation criteria for agents:
/// - Correctness: Does it produce accurate outputs?
/// - Robustness: Does it handle edge cases and errors gracefully?
/// - Schema Compliance: Does it validate against JSON schemas correctly?
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
    public async Task Test_BasicSchemaValidation_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var inputPath = Path.Combine(_testDataPath, "person-input.json");

        // Act
        var (exitCode, output, error) = await RunAgentAsync(schemaPath, inputPath);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("John Doe", output);
        Assert.Contains("30", output);
        Assert.Empty(error);
    }

    [Fact]
    public async Task Test_NestedObjectValidation_ComplexSchema_ReturnsSuccess()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "nested-schema.json");
        var inputPath = Path.Combine(_testDataPath, "nested-input.json");

        // Act
        var (exitCode, output, error) = await RunAgentAsync(schemaPath, inputPath);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Jane Smith", output);
        Assert.Contains("Springfield", output);
        Assert.Contains("developer", output);
    }

    [Fact]
    public async Task Test_StringLiteralInput_ValidJson_ReturnsSuccess()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var jsonInput = "{\"name\":\"Alice\",\"age\":25}";

        // Act
        var (exitCode, output, error) = await RunAgentAsync(schemaPath, jsonInput);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Alice", output);
        Assert.Contains("25", output);
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
    public async Task Test_MissingRequiredField_ReturnsValidationError()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var jsonInput = "{\"age\":25}"; // Missing required "name" field

        // Act
        var (exitCode, output, error) = await RunAgentAsync(schemaPath, jsonInput);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("Validation failed", output);
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
    public async Task Test_Performance_CompleteWithinTimeLimit()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "nested-schema.json");
        var inputPath = Path.Combine(_testDataPath, "nested-input.json");
        var stopwatch = Stopwatch.StartNew();

        // Act
        var (exitCode, output, error) = await RunAgentAsync(schemaPath, inputPath);
        stopwatch.Stop();

        // Assert - should complete within 5 seconds
        Assert.Equal(0, exitCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, 
            $"Agent took too long: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Test_ArrayHandling_ValidatesArrayItems()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "nested-schema.json");
        var jsonInput = "{\"tags\":[\"tag1\",\"tag2\",\"tag3\"]}";

        // Act
        var (exitCode, output, error) = await RunAgentAsync(schemaPath, jsonInput);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("tag1", output);
        Assert.Contains("tag2", output);
        Assert.Contains("tag3", output);
    }

    [Fact]
    public async Task Test_Inference_HandlesNumberTypes()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var jsonInput = "{\"name\":\"Bob\",\"age\":42}";

        // Act
        var (exitCode, output, error) = await RunAgentAsync(schemaPath, jsonInput);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("42", output);
    }

    [Fact]
    public async Task Test_EmptyObjectValidation_ReturnsError()
    {
        // Arrange
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var jsonInput = "{}"; // Empty object, missing required "name"

        // Act
        var (exitCode, output, error) = await RunAgentAsync(schemaPath, jsonInput);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("Validation failed", output);
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

