using AgenticStructuredOutput.Tests.Harness;
using System.Text.Json.Nodes;

namespace AgenticStructuredOutput.Tests;

/// <summary>
/// Integration tests for the data mapping agent.
/// Tests validate that the agent correctly maps arbitrary input JSON to the target schema.
/// Inherits common setup and invocation logic from AgentTestHarness.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.None)]  // Run sequentially
public class AgentIntegrationTests : AgentTestHarness
{
    // All setup logic inherited from AgentTestHarness ([OneTimeSetUp] SetupDefaultSchema)

    private static IEnumerable<TestCaseData> LoadIntegrationTestCasesFromJsonL()
    {
        var filePath = GetTestResourcePath("test-cases-integration.jsonl");
        return TestDataLoader.LoadIntegrationTestCases(filePath);
    }

    [Test]
    [CancelAfter(MaxTestTimeoutMs)]
    [TestCaseSource(nameof(LoadIntegrationTestCasesFromJsonL))]
    public async Task Agent_ShouldMapInputSuccessfully(string testScenario, string input, string expectedJsonOutput)
    {
        await LogAsync($"Starting test scenario: {testScenario}");
        
        // Arrange
        var prompt = $"Map the following input to the schema:\nInput:\n{input}";

        // Act
        await LogAsync($"Invoking agent for scenario: {testScenario}");
        var response = await InvokeAgentAsync(prompt);

        // Assert
        await LogAsync($"Received response of {response?.Length ?? 0} characters for scenario: {testScenario}");
        Assert.That(response, Is.Not.Null, "Response should not be null");
        Assert.That(response, Is.Not.Empty, "Response should not be empty");
        Assert.That(response.Trim(), Is.Not.Empty, "Response should not be whitespace only");

        await LogAsync($"Parsing response JSON for scenario: {testScenario}");
        await LogAsync($"Response content: {response}");
        var responseJson = JsonNode.Parse(response) ?? throw new InvalidOperationException("Response JSON could not be parsed");
        var expectedJson = JsonNode.Parse(expectedJsonOutput) ?? throw new InvalidOperationException("Expected JSON could not be parsed");

        foreach(var property in expectedJson.AsObject())
        {
            var responseProperty = responseJson![property.Key];
            var areEqual = responseProperty != null && responseProperty.ToJsonString() == property.Value?.ToJsonString();
            Assert.That(areEqual,
                Is.True,
                $"Property '{property.Key}' should match expected value in scenario: {testScenario}");
        }

        await LogAsync($"Test scenario passed: {testScenario}");
    }
}
