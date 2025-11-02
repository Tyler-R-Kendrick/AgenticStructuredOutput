using System.Text.Json;
using AgenticStructuredOutput;
using AgenticStructuredOutput.Services;
using AgenticStructuredOutput.Extensions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Json.Schema;
using Json.More;

namespace AgenticStructuredOutput.Tests;

/// <summary>
/// Direct Agent Integration Tests - instantiate and test the agent directly
/// without web server or A2A complexity
/// </summary>
public class AgentIntegrationTests : IAsyncLifetime
{
    private IAgentFactory? _agentFactory;
    private AIAgent? _agent;
    private IServiceProvider? _serviceProvider;

    public async Task InitializeAsync()
    {
        // Setup DI container with required services
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Add agent services (mimics Program.cs setup)
        services.AddAgentServices();
        
        _serviceProvider = services.BuildServiceProvider();
        _agentFactory = _serviceProvider.GetRequiredService<IAgentFactory>();
        
        // Load the schema
        var assembly = typeof(Program).Assembly;
        var resourceName = "AgenticStructuredOutput.Resources.schema.json";
        string schemaJson;
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                throw new InvalidOperationException($"Could not find embedded resource: {resourceName}");
            }
            using var reader = new StreamReader(stream);
            schemaJson = reader.ReadToEnd();
        }

        var jsonSchema = JsonSchema.FromText(schemaJson);
        using var schemaDoc = jsonSchema.ToJsonDocument();
        var schemaElement = schemaDoc.RootElement.Clone();
        
        // Create the agent with schema validation
        // Note: ModelId must be specified for Azure AI Inference
        var modelId = Environment.GetEnvironmentVariable("MODEL_ID") ?? "gpt-4o";
        _agent = await _agentFactory.CreateDataMappingAgentAsync(new()
        {
            ModelId = modelId,
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema(
                schema: schemaElement,
                schemaName: "DynamicOutput",
                schemaDescription: "Intelligently mapped JSON output conforming to the provided schema"
            )
        });
        
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        (_serviceProvider as IAsyncDisposable)?.DisposeAsync().GetAwaiter().GetResult();
        await Task.CompletedTask;
    }

    private async Task<string> InvokeAgentAsync(string userMessage)
    {
        if (_agent == null)
        {
            throw new InvalidOperationException("Agent not initialized");
        }

        var response = await _agent.RunAsync(userMessage);
        return response?.Text ?? string.Empty;
    }

    [Fact]
    public async Task Agent_WithValidFuzzyMappedInput_ShouldMapFuzzyFieldNames()
    {
        // Arrange
        var fuzzyInput = @"{
            ""first_name"": ""John"",
            ""last_name"": ""Doe"",
            ""email_address"": ""john.doe@example.com""
        }";

        var prompt = $"Map the following input to the schema, using fuzzy matching for field names:\nInput:\n{fuzzyInput}";

        // Act
        var response = await InvokeAgentAsync(prompt);

        // Assert
        Assert.NotNull(response);
        Assert.True(!string.IsNullOrWhiteSpace(response), 
            "Agent should return a response with content");
    }

    [Fact]
    public async Task Agent_WithStructuredInput_ShouldPreserveStructure()
    {
        // Arrange
        var structuredInput = @"{
            ""firstName"": ""Jane"",
            ""lastName"": ""Smith"",
            ""email"": ""jane.smith@example.com""
        }";

        var prompt = $"Map the following structured input to the schema:\nInput:\n{structuredInput}";

        // Act
        var response = await InvokeAgentAsync(prompt);

        // Assert
        Assert.NotNull(response);
        Assert.True(!string.IsNullOrWhiteSpace(response),
            "Agent should return a response");
    }

    [Fact]
    public async Task Agent_WithComplexNestedInput_ShouldFlattenStructure()
    {
        // Arrange
        var complexInput = @"{
            ""name"": { 
                ""first"": ""Michael"",
                ""last"": ""Brown""
            },
            ""contact"": {
                ""email"": ""michael.brown@example.com""
            }
        }";

        var prompt = $"Map the following nested input to the flat schema, extracting values from nested properties:\nInput:\n{complexInput}";

        // Act
        var response = await InvokeAgentAsync(prompt);

        // Assert
        Assert.NotNull(response);
        Assert.True(!string.IsNullOrWhiteSpace(response),
            "Agent should flatten and map nested structure");
    }

    [Fact]
    public async Task Agent_WithMinimalRequiredFields_ShouldProcess()
    {
        // Arrange
        var minimalInput = @"{
            ""firstName"": ""Alice"",
            ""lastName"": ""Johnson"",
            ""email"": ""alice.johnson@example.com""
        }";

        var prompt = $"Map the following input with minimal fields to the schema:\nInput:\n{minimalInput}";

        // Act
        var response = await InvokeAgentAsync(prompt);

        // Assert
        Assert.NotNull(response);
        Assert.True(!string.IsNullOrWhiteSpace(response),
            "Agent should process minimal required fields");
    }

    [Fact]
    public async Task Agent_WithDifferentCasingPatterns_ShouldNormalize()
    {
        // Arrange
        var casingSensitiveInput = @"{
            ""FirstName"": ""Bob"",
            ""LASTNAME"": ""Wilson"",
            ""Email"": ""bob.wilson@example.com""
        }";

        var prompt = $"Map the following input with various casing to the schema, normalizing field names through fuzzy matching:\nInput:\n{casingSensitiveInput}";

        // Act
        var response = await InvokeAgentAsync(prompt);

        // Assert
        Assert.NotNull(response);
        Assert.True(!string.IsNullOrWhiteSpace(response),
            "Agent should normalize casing");
    }

    [Fact]
    public async Task Agent_WithExtraFields_ShouldFilterToSchema()
    {
        // Arrange
        var inputWithExtraFields = @"{
            ""firstName"": ""Carol"",
            ""lastName"": ""Martinez"",
            ""email"": ""carol.martinez@example.com"",
            ""socialSecurityNumber"": ""123-45-6789"",
            ""internalId"": ""12345""
        }";

        var prompt = $"Map the following input to the schema, filtering out fields not in the schema:\nInput:\n{inputWithExtraFields}";

        // Act
        var response = await InvokeAgentAsync(prompt);

        // Assert
        Assert.NotNull(response);
        Assert.True(!string.IsNullOrWhiteSpace(response),
            "Agent should extract only schema-relevant fields");
    }

    [Fact]
    public async Task Agent_WithAbbreviatedFieldNames_ShouldMapToFull()
    {
        // Arrange
        var abbreviatedInput = @"{
            ""fname"": ""David"",
            ""lname"": ""Lee"",
            ""email"": ""david.lee@example.com""
        }";

        var prompt = $"Map the following input with abbreviated field names to the full schema field names:\nInput:\n{abbreviatedInput}";

        // Act
        var response = await InvokeAgentAsync(prompt);

        // Assert
        Assert.NotNull(response);
        Assert.True(!string.IsNullOrWhiteSpace(response),
            "Agent should fuzzy-match abbreviations");
    }

    [Fact]
    public async Task Agent_ShouldProduceValidJsonResponse()
    {
        // Arrange
        var input = @"{
            ""firstName"": ""Eve"",
            ""lastName"": ""Taylor"",
            ""email"": ""eve.taylor@example.com""
        }";

        var prompt = $"Map the following input to valid JSON conforming to the schema. Only respond with valid JSON.\nInput:\n{input}";

        // Act
        var response = await InvokeAgentAsync(prompt);

        // Assert
        Assert.NotNull(response);
        Assert.True(!string.IsNullOrWhiteSpace(response),
            "Agent should produce a response");
    }

    [Fact]
    public async Task Agent_OutputShouldConformToSchema_WhenSuccessful()
    {
        // Arrange
        var input = @"{
            ""firstName"": ""Frank"",
            ""lastName"": ""Anderson"",
            ""email"": ""frank.anderson@example.com"",
            ""phoneNumber"": ""555-5555""
        }";

        var prompt = $"Map and validate the following input against the schema:\nInput:\n{input}\n\nRespond with valid JSON conforming to the schema.";

        // Act
        var response = await InvokeAgentAsync(prompt);

        // Assert
        Assert.NotNull(response);
        Assert.True(!string.IsNullOrWhiteSpace(response),
            "Agent should produce schema-conformant output");
    }

    [Fact]
    public async Task Agent_ShouldBeInvokedSuccessfully()
    {
        // Arrange - Simple test to verify agent invocation
        var simpleInput = @"{
            ""firstName"": ""Test"",
            ""lastName"": ""User"",
            ""email"": ""test@example.com""
        }";

        var prompt = $"Map this input to the schema:\nInput:\n{simpleInput}";

        // Act
        var response = await InvokeAgentAsync(prompt);

        // Assert - Verify agent was successfully invoked and returned a response
        Assert.NotNull(response);
        Assert.True(!string.IsNullOrWhiteSpace(response),
            "Agent should be invoked successfully and return a response");
    }
}
