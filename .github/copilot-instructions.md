# Copilot Instructions for AgenticStructuredOutput

## Project Overview
**AgenticStructuredOutput** is an A2A (Agent-to-Agent) hosted service using **Microsoft Agent Framework** that intelligently maps arbitrary JSON input to a target schema at runtime. The agent leverages **Azure AI Inference** (GitHub Models) with fuzzy logic to perform semantic field mapping.

**Key Innovation**: Dynamic JSON schema provided per-request (not hardcoded), enabling flexible structured output.

## Critical Architecture Patterns

### 1. Agent Framework Flow (Microsoft.Agents.AI)
```
Client Request (Input JSON + Schema)
    ↓ 
ChatCompletionsClient (Azure.AI.Inference → GitHub Models)
    ↓
AIAgent (with ChatResponseFormat.ForJsonSchema for structured output)
    ↓
Schema Validation (JsonSchema.Net)
    ↓
Response
```

**Key Decision**: Agents are created per-request with dynamic schema config via `ChatOptions`, not reused singletons.

### 2. Dynamic Schema Loading Pattern
- Schema is **not hardcoded** into the agent—it's provided with each request
- `ChatResponseFormat.ForJsonSchema(schema, schemaName)` configures structured output at agent creation time
- Use `JsonNode.Parse()` for dynamic JSON manipulation, not strong typing
- Always validate agent output against the provided schema using `JsonSchema.FromText().Evaluate()`

### 3. Dependency Injection Strategy
- **Singleton**: `IAgentFactory` (creates agents per-request internally)
- **Singleton**: `IChatClient` (underlying model access via Azure AI Inference)
- **Per-Request**: `AIAgent` instances (built with request-specific schema)

Setup in `Program.cs`:
```csharp
builder.Services.AddAgentServices(); // Registers factory and chat client
var agentFactory = scope.ServiceProvider.GetRequiredService<IAgentFactory>();
agent = await agentFactory.CreateDataMappingAgentAsync(new() { 
    ResponseFormat = ChatResponseFormat.ForJsonSchema(schemaElement, "DynamicOutput")
});
```

## Critical Patterns & Conventions

### ChatCompletionsClient Initialization (Azure.AI.Inference)
Use the **AzureInferenceChatClientBuilder** helper (located in `AgenticStructuredOutput.Tests/Builders/`):
```csharp
var client = new AzureInferenceChatClientBuilder()
    .UseGitHubModelsEndpoint()
    .WithEnvironmentApiKey()
    .BuildChatCompletionsClient();
```

**Do NOT** create `ChatCompletionsClient` directly—use the builder for consistent endpoint/auth handling.

### API Key Resolution
Environment variable priority: `GITHUB_TOKEN` > `OPENAI_API_KEY`
- Use `ApiKeyHelper` (test helpers) for resolution in tests
- In application code: `ServiceCollectionExtensions.AddAgentServices()` handles this automatically
- GitHub Actions automatically provides `GITHUB_TOKEN`, enabling seamless CI/CD

### Integration Testing Pattern
Tests that call real GitHub Models API should gracefully skip when the API is unavailable:
```csharp
catch (Azure.RequestFailedException ex) when (ex.Status == 404 || ex.Status == 401 || ex.Status == 403)
{
    Assert.Ignore($"Skipping integration test - GitHub Models not accessible (HTTP {ex.Status})");
}
```

This ensures CI/CD doesn't fail when external APIs are unreachable.

## File Organization & Key Components

### Core Service Files
- **`Program.cs`**: A2A endpoint setup, schema loading, request/response validation
- **`Extensions/ServiceCollectionExtensions.cs`**: DI registration (API key resolution, chat client creation)
- **`Services/AgentFactory.cs`**: Creates `AIAgent` with dynamic schema config
- **`Services/AgentInstructions.cs`**: System prompts for the agent ("DataMappingExpert")

### Test Helpers (Reusable Abstractions)
- **`Tests/Builders/AzureInferenceChatClientBuilder.cs`**: Fluent builder for client creation
- **`Tests/Helpers/DependencyInjectionHelper.cs`**: DI container helpers
- **`Tests/Helpers/ApiKeyHelper.cs`**: API key resolution from environment
- **`Tests/ChatCompletionsClientInitializationTests.cs`**: 14 test patterns demonstrating patterns (12 passing, 2 skipped integration tests)

### Data & Configuration
- **`Resources/schema.json`**: Embedded resource with default schema structure
- **`appsettings.json`**: App configuration (logging levels, port, etc.)

## Build & Test Workflow

### Build
```bash
dotnet build                                    # Build solution
dotnet build AgenticStructuredOutput.slnx       # Explicit solution file
```

### Test

If tests fail, don't report them as passing.
NEVER force-pass tests or skip them; this includes integration tests that hit external APIs.
Integration tests should surface errors.

```bash
dotnet test                                     # Run all tests (12 pass, 2 skip)
dotnet test --filter "ChatCompletionsClientInitialization"  # Run specific test class
```
**Key Insight**: Tests skip when GitHub Models API is unreachable—this is intentional for CI/CD resilience.

### Run
```bash
export GITHUB_TOKEN="ghp_..."  # Required for GitHub Models
dotnet run                      # Server starts on http://localhost:5000
```

## Project-Specific Conventions

### Schema Validation Pattern
Always use `JsonSchema.Net` for validation:
```csharp
var compiled = JsonSchema.FromText(schemaJson);
var validation = compiled.Evaluate(outputNode);
if (!validation.IsValid) throw new InvalidOperationException("Schema validation failed");
```

### Agent Instructions
Agent behavior is driven by **explicit system prompts** in `AgentInstructions.cs`. Key instructions include:
- Fuzzy matching for field mapping ("fullName" → "name", "yearsOld" → "age")
- Type inference based on schema requirements
- Nested structure handling

When modifying agent behavior, update `AgentInstructions.DataMappingExpert` string constant, then recreate agents.

### Error Handling in Program.cs
- **Invalid Request JSON**: Return 400 Bad Request (parsed via `RequestParser` helper)
- **Invalid Schema JSON**: Return 400 Bad Request
- **Agent Output Invalid JSON**: Return 500 Problem (agent error)
- **Schema Validation Failure**: Return 500 Problem (output doesn't match schema)

Use the `RequestParser` and `ResponseValidator` static helper classes for parsing/validation logic.

### DTO Classes
- **`AgentRequest`**: Input model with `Input` (JSON string) and `Schema` (JSON schema string) properties
- **`HealthResponse`**: Simple health check response
- **`AgentInfo`**: Agent metadata (used by A2A hosting)

## Testing Guidelines

### Unit Tests (Don't Hit API)
Most tests use `CreateForTesting()` factory with placeholder credentials—these should all pass:
```csharp
[Test]
public void TestExample()
{
    var client = AzureInferenceChatClientBuilder.CreateForTesting()
        .BuildIChatClient();
    Assert.That(client, Is.Not.Null);
}
```

### Integration Tests (Hit Real API)
Marked with `[Category("Integration")]` and require valid `GITHUB_TOKEN`:
- Only run when credentials are available
- Use exception handling to skip gracefully if API is unreachable
- Example: `ChatCompletionsClientInitializationTests` has 2 such tests (currently skipped in CI)

## Dependencies to Know

- **Microsoft.Agents.AI** (1.0.0-preview): Agent Framework, A2A hosting
- **Azure.AI.Inference** (1.0.0-beta.5): ChatCompletionsClient for GitHub Models
- **JsonSchema.Net** (7.4.0): Schema validation
- **Microsoft.Extensions.AI**: IChatClient abstractions
- **.NET 9.0**: Runtime

No external APIs beyond GitHub Models (via Azure SDK).

## Common Pitfalls & Solutions

| Issue | Solution |
|-------|----------|
| "No API key found" exception | Set `GITHUB_TOKEN` or `OPENAI_API_KEY` env var |
| 404 from GitHub Models endpoint | Endpoint is correct; issue may be auth or rate limiting |
| Schema validation fails on agent output | Agent returned valid JSON but didn't match schema structure—check `AgentInstructions` |
| Tests fail in CI/CD | This is expected for integration tests; use `Assert.Ignore()` to skip when API unavailable |
| Strong typing breaks with dynamic schema | Use `JsonNode` for dynamic JSON, not `[JsonSerializable]` classes |

## Next Steps for Contributors

1. **Understand the flow**: Read `Program.cs` → `AgentFactory.cs` → `ServiceCollectionExtensions.cs`
2. **Explore test patterns**: See `ChatCompletionsClientInitializationTests.cs` for builder/DI examples
3. **Modify agent behavior**: Update `AgentInstructions.DataMappingExpert` and test with custom schemas
4. **Add new endpoints**: Follow the request parsing → agent creation → response validation pattern in `Program.cs`
5. **Debug**: Use `ILogger<T>` injected in factories and services; logs include agent creation and validation steps
