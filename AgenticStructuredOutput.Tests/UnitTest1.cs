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
        var testAssemblyPath = typeof(AgentEvaluationTests).Assembly.Location;
        var testDir = Path.GetDirectoryName(testAssemblyPath) ?? Directory.GetCurrentDirectory();
        
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
    public async Task Test_FuzzyMapping_AgentInference()
    {
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var inputPath = Path.Combine(_testDataPath, "person-input-fuzzy.json");

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return; // Skip test if no API key
        }

        var (exitCode, output, error) = await RunAgentAsync(schemaPath, inputPath);

        Assert.Equal(0, exitCode);
        Assert.Contains("John Doe", output);
        Assert.Contains("30", output);
        Assert.Contains("john.doe@example.com", output);
    }

    [Fact]
    public async Task Test_StringLiteralInput()
    {
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var jsonInput = "{\"firstName\":\"Alice\",\"ageInYears\":25}";

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return; // Skip test
        }

        var (exitCode, output, error) = await RunAgentAsync(schemaPath, jsonInput);

        Assert.Equal(0, exitCode);
        Assert.Contains("Alice", output);
    }

    [Fact]
    public async Task Test_InvalidJson_ReturnsError()
    {
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var invalidJson = "{invalid json}";

        var (exitCode, output, error) = await RunAgentAsync(schemaPath, invalidJson);

        Assert.Equal(1, exitCode);
        Assert.Contains("Invalid JSON", output);
    }

    [Fact]
    public async Task Test_NoArguments_ShowsUsage()
    {
        var (exitCode, output, error) = await RunAgentAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("Usage:", output);
    }

    [Fact]
    public async Task Test_NoApiKey_ShowsError()
    {
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var inputPath = Path.Combine(_testDataPath, "person-input-fuzzy.json");
        
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

            var (exitCode, output, error) = await RunAgentAsync(schemaPath, inputPath);

            Assert.Equal(1, exitCode);
            Assert.Contains("No API key found", output);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public async Task Test_Performance_CompleteWithinTimeLimit()
    {
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var inputPath = Path.Combine(_testDataPath, "person-input-fuzzy.json");

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return; // Skip test
        }

        var stopwatch = Stopwatch.StartNew();

        var (exitCode, output, error) = await RunAgentAsync(schemaPath, inputPath);
        stopwatch.Stop();

        Assert.Equal(0, exitCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 30000, 
            $"Agent took too long: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Test_OutputIsValidJson()
    {
        var schemaPath = Path.Combine(_testDataPath, "person-schema.json");
        var jsonInput = "{\"personName\":\"Bob\",\"yearsOfAge\":42}";

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return; // Skip test
        }

        var (exitCode, output, error) = await RunAgentAsync(schemaPath, jsonInput);

        Assert.Equal(0, exitCode);
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

