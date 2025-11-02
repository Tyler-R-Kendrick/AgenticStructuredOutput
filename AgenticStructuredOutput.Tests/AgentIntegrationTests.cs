using AgenticStructuredOutput.Services;
using AgenticStructuredOutput.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.More;

namespace AgenticStructuredOutput.Tests;

[TestFixture]
[Parallelizable(ParallelScope.None)]  // Run sequentially
public class AgentIntegrationTests
{
    private JsonElement? _defaultSchemaElement;

    [OneTimeSetUp]
    public void LoadDefaultSchema()
    {
        // Load schema from the main project's embedded resource
        var assembly = typeof(AgentFactory).Assembly;  // Get the main project assembly
        var resourceName = "AgenticStructuredOutput.Resources.schema.json";
        using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException($"Could not find embedded resource: {resourceName}");
        using var reader = new StreamReader(stream);
        var schemaJson = reader.ReadToEnd();
        var schemaDoc = JsonDocument.Parse(schemaJson);
        _defaultSchemaElement = schemaDoc.RootElement.Clone();
    }

    private async Task<string> InvokeAgentAsync(string userMessage, JsonElement? overrideSchema = null)
    {        
        try
        {
            // Setup DI container with required services
            await LogAsync("Setting up DI container...");
            var services = new ServiceCollection();
            
            // Add logging
            services.AddLogging(builder => builder.AddConsole());

            // Add agent services (mimics Program.cs setup)
            await LogAsync($"GITHUB_TOKEN set: {!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_TOKEN"))}");
            await LogAsync($"OPENAI_API_KEY set: {!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))}");
            await LogAsync($"MODEL_ID: {Environment.GetEnvironmentVariable("MODEL_ID") ?? "openai/gpt-4o-mini (default)"}");
            
            services.AddAgentServices();
            
            var serviceProvider = services.BuildServiceProvider();            
            var factory = serviceProvider.GetRequiredService<IAgentFactory>();
            
            var modelId = Environment.GetEnvironmentVariable("MODEL_ID") ?? "openai/gpt-4o-mini";
            await LogAsync($"Creating agent with model: {modelId}");

            var schemaElement = overrideSchema ?? _defaultSchemaElement;
            if (schemaElement == null)
            {
                throw new InvalidOperationException("Schema element is not initialized");
            }

            await LogAsync("Creating agent with JSON schema response format...");

            var agent = await factory.CreateDataMappingAgentAsync(new()
            {
                ModelId = modelId,
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    schema: schemaElement.Value,
                    schemaName: "DynamicOutput",
                    schemaDescription: "Intelligently mapped JSON output conforming to the provided schema"
                )
            });

            await LogAsync($"Invoking agent with user message...");
            await LogAsync($"Message: {userMessage[..Math.Min(100, userMessage.Length)]}...");
            
            // Execute agent - timeout is handled by NUnit's CancelAfter attribute
            var response = await agent.RunAsync(userMessage, cancellationToken: TestContext.CurrentContext.CancellationToken);
            
            return response?.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            await LogAsync($"Error: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
            {
                await LogAsync($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
            throw;
        }
    }

    private static async Task LogAsync(string message)
    {
        await TestContext.Out.WriteLineAsync($"[TEST] {message}");
    }

    [Test]
    [CancelAfter(5000)]  // 3 second timeout
    [TestCase(
        "Fuzzy matching",
        """
        {
            "first_name": "John",
            "last_name": "Doe",
            "email_address": "john.doe@example.com"
        }
        """,
        """
        {
            "firstName": "John",
            "lastName": "Doe",
            "email": "john.doe@example.com"
        }
        """
    )]
    
    [TestCase(
        "Nested extraction",
        """
        {
            "name": { 
                "first": "Michael",
                "last": "Brown"
            },
            "contact": {
                "email": "michael.brown@example.com"
            }
        }
        """,
        """
        {
            "firstName": "Michael",
            "lastName": "Brown",
            "email": "michael.brown@example.com"
        }
        """
    )]
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
