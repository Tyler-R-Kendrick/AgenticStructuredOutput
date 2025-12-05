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
Use the **AzureInferenceChatClientBuilder** helper (located in `AgenticStructuredOutput/Extensions/`):
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

Test results are automatically collected to `TestResults/` directory as `.trx` (Test Results XML) files:

```bash
dotnet test --settings .runsettings                                     # Run all tests with TRX output
dotnet test --filter "ChatCompletionsClientInitialization"             # Run specific test class
```

#### Test Artifacts & Result References

After running tests, results are saved to `TestResults/*.trx`. **Before rerunning tests**, check for cached results:

1. **List latest test run**: `ls -lt TestResults/ | head -5`
2. **Read test results**: Parse the `.trx` XML file to extract:
   - Test outcomes (Passed, Failed, Skipped)
   - Error messages and stack traces
   - Test duration and timestamps
3. **Detect failures in console output**: If test output contains error indicators (FAIL, Error, INCORRECT, UNGROUNDED, "score too low"), mark test as failed regardless of formal pass/fail status

**Key Pattern**: Before proposing to rerun tests, first check `TestResults/` for latest `.trx` file and parse it. Use artifact data to understand previous failures instead of immediately rerunning.

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

### Test Result Artifacts (.TRX Files)

Test results are automatically saved as `.trx` (XML) files in `TestResults/` directory when running:
```bash
dotnet test --settings .runsettings
```

**Before rerunning tests, always check the latest `.trx` artifact:**

1. **Find latest results**: 
   ```bash
   ls -lt TestResults/*.trx | head -1
   ```

2. **Parse test outcomes** from the `.trx` file to extract:
   - Individual test pass/fail/skip status
   - Error messages and stack traces  
   - Test execution timestamps and duration
   - Console output captured during test execution

3. **Monitor console output for error indicators**:
   - Look for patterns: `FAIL`, `Error`, `ERROR`, `Exception`, `INCORRECT`, `UNGROUNDED`, `"score too low"`, `failed`, `FAILED`
   - If these patterns appear in test output, mark test as **FAILED** even if formal status shows Passed
   - This catches LLM-based evaluation failures that may not surface as assertion failures

## Test Triage Workflow - Use TRX First, Rerun Last

**CRITICAL PATTERN: Never immediately rerun tests without analyzing the TRX artifact first. Use existing test results to triage failures.**

### Step-by-step triage process:

1. **Check for existing TRX artifacts** before running any tests:
   ```bash
   ls -lrt TestResults/*.trx | tail -1
   ```
   - If artifacts exist and are recent (< 1 hour old), use them for analysis
   - Only rerun if artifacts are stale or if you're making changes that warrant re-evaluation

2. **Extract failure data from latest TRX**:
   ```bash
   # Count test outcomes
   grep "outcome=" TestResults/TestResults.trx | grep -o 'outcome="[^"]*"' | sort | uniq -c
   
   # Extract error messages
   grep -A 3 "<ErrorInfo>" TestResults/TestResults.trx
   
   # List all failed tests
   grep 'outcome="Failed"' TestResults/TestResults.trx | grep -o 'testName="[^"]*"'
   ```

3. **Analyze each failure from TRX**:
   - Extract the test name and error message
   - Categorize: Is it a timeout? LLM evaluation failure? API rate limit? Schema validation?
   - Document the root cause before making any code changes

4. **Create fix plan**:
   - For each failed test, identify what needs to be fixed:
     - Agent logic issue? → Update `AgentInstructions.cs` or agent behavior
     - Test timeout? → Increase `[CancelAfter]` timeout (30 seconds for LLM calls)
     - Validation failure? → Review schema or test expectations
     - API unavailable? → Verify credentials and network
   - Do NOT make changes speculatively; be guided by TRX error analysis

5. **Implement fixes** based on TRX analysis:
   - Make targeted changes to address specific failures
   - Update only what's necessary to fix the identified issues

6. **Rerun tests ONLY after all fixes are implemented**:
   - After you've addressed every issue identified in the TRX
   - Generate fresh TRX artifacts with `dotnet test --settings .runsettings`
   - Compare new outcomes against previous TRX to verify improvements

### Example TRX triage session:

```
Previous TRX shows: Failed: 3, Passed: 10, Skipped: 0
├─ Correctness test 1: FAILED - "Agent output includes extra null fields"
├─ Correctness test 2: FAILED - "Agent output includes extra null fields"
└─ Integration test: FAILED - "HTTP 429 Too Many Requests"

Fix plan:
1. Update agent to not include null fields → Fix AgentInstructions
2. Add retry logic for 429 errors → Update InvokeAgentAsync()

After fixes → Rerun once → Compare new TRX results
```

**Critical Pattern**: When asked to verify or rerun tests:
1. Check for existing `.trx` in `TestResults/`
2. Parse the latest file to understand previous outcomes
3. Use artifact data to explain what already failed/passed
4. Only rerun if artifacts are stale (> 1 hour old) or if specific changes warrant re-evaluation
5. Always check console output for error indicators before concluding a test passed

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
| Integration test timeout | **CRITICAL: NEVER skip tests due to timeout.** Increase `CancelAfter` timeout instead. Timeouts indicate real problems (slow network, API issues). See "Test Timeout Handling" section below. |
| Strong typing breaks with dynamic schema | Use `JsonNode` for dynamic JSON, not `[JsonSerializable]` classes |

## Test Timeout Handling - NEVER Skip Tests

**ABSOLUTE RULE: Integration tests MUST NEVER be skipped due to timeout or `OperationCanceledException`.** This hides real problems and breaks the test suite.

**When you encounter timeout failures:**

1. **Increase the timeout** - Integration tests legitimately need more time for API calls
   - LLM inference + network latency can exceed 3-5 seconds
   - Use `[CancelAfter(30000)]` or higher for integration tests hitting external APIs
   - Example: `[CancelAfter(MaxIntegrationTestTimeoutMs)]` with `private const int MaxIntegrationTestTimeoutMs = 30000;`

2. **Do NOT catch and skip** - This anti-pattern hides failures:
   - ❌ DON'T: `catch (OperationCanceledException) { Assert.Ignore(...); }`
   - ❌ DON'T: `catch (TaskCanceledException) { Assert.Ignore(...); }`
   - ❌ DON'T: `catch (TimeoutException) { Assert.Ignore(...); }`
   - These masks real issues: slow infrastructure, API problems, network degradation

3. **Only skip for API unavailability (HTTP errors)** - Skip ONLY when:
   - HTTP 401/403/404 from GitHub Models (authentication/availability issue)
   - The API is genuinely unreachable, not slow
   - Example: `catch (Azure.RequestFailedException ex) when (ex.Status == 404 || ex.Status == 401 || ex.Status == 403)`

4. **Report timeouts as failures** - Timeout = test failure, not test skip
   - Let the test fail and surface the issue
   - This alerts developers to infrastructure problems
   - CI/CD can be configured to tolerate timeout failures, but skipping hides them

**Why this matters:**
- Skipped tests give false confidence (all green ✓)
- Timeouts indicate real problems that need investigation
- Hiding timeouts breaks debugging and root cause analysis
- Performance regressions go undetected

**Example of correct timeout handling:**
```csharp
[Test]
[CancelAfter(MaxIntegrationTestTimeoutMs)]  // 30 seconds for API calls
public async Task Agent_IntegrationTest()
{
    try
    {
        var response = await _agent.RunAsync(userMessage);
        Assert.That(response, Is.Not.Null);
    }
    catch (Azure.RequestFailedException ex) when (ex.Status == 404 || ex.Status == 401 || ex.Status == 403)
    {
        // ONLY skip if API genuinely unreachable
        Assert.Ignore($"Skipping - GitHub Models API not accessible (HTTP {ex.Status})");
    }
    // Let OperationCanceledException, TaskCanceledException, etc. fail the test
}
```

## Next Steps for Contributors

1. **Understand the flow**: Read `Program.cs` → `AgentFactory.cs` → `ServiceCollectionExtensions.cs`
2. **Explore test patterns**: See `ChatCompletionsClientInitializationTests.cs` for builder/DI examples
3. **Modify agent behavior**: Update `AgentInstructions.DataMappingExpert` and test with custom schemas
4. **Add new endpoints**: Follow the request parsing → agent creation → response validation pattern in `Program.cs`
5. **Debug**: Use `ILogger<T>` injected in factories and services; logs include agent creation and validation steps
