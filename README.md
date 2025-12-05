# AgenticStructuredOutput

A2A (Agent-to-Agent) hosted service using **Microsoft Agent Framework** for intelligent JSON data mapping with fuzzy logic and semantic inference, powered by **Azure AI Inference** with GitHub Models. Supports **dynamic JSON schema** at runtime.

## Overview

This implementation uses the **Microsoft Agent Framework** with A2A (Agent-to-Agent) hosting to create a web service that intelligently maps JSON input to a target schema. The agent uses **Azure AI Inference** to access GitHub Models with **fuzzy logic** and **inference** for semantic field mapping. The schema is provided dynamically with each request, allowing flexible structured output.

## Key Features

- **Microsoft Agent Framework**: Uses official `Microsoft.Agents.AI` library
- **A2A Hosting**: ASP.NET Core web service with `Microsoft.Agents.AI.Hosting.A2A.AspNetCore`
- **Azure AI Inference**: GitHub Models access via `Azure.AI.Inference`
- **GITHUB_TOKEN Authentication**: Uses GitHub token for model access (works in GitHub Actions)
- **Dynamic JSON Schema**: Accept schema at runtime with each request (no hardcoded output types)
- **Schema Validation**: Uses `JsonSchema.Net` to validate agent output against provided schema
- **AI-Powered Fuzzy Mapping**: Intelligently maps fields like "fullName" → "name"
- **Expert Agent Instructions**: Agent configured as data mapping expert
- **🆕 Auto-Prompt-Optimization**: Automatically improves agent prompts based on evaluation metrics (see `AgenticStructuredOutput.Optimization`)

## Projects

This repository contains multiple projects:

- **AgenticStructuredOutput**: Main A2A service for JSON schema mapping
- **AgenticStructuredOutput.Resources**: Shared embedded resources (schema, prompts, test cases)
- **AgenticStructuredOutput.Tests**: Test suite with evaluation framework
- **🆕 AgenticStructuredOutput.Optimization**: Auto-prompt-optimization library
- **🆕 AgenticStructuredOutput.Simulation**: Eval generation library (optional)
- **🆕 AgenticStructuredOutput.Optimization.CLI**: CLI tool for running optimization experiments

## Architecture

```
Client Request (JSON + Schema)
    ↓
ASP.NET Core Server (A2A Hosting)
    ↓
AI Agent (Microsoft Agent Framework)
    - Dynamic Schema Configuration
    - ChatResponseFormat.ForJsonSchema()
    ↓
Azure AI Inference (GitHub Models via GITHUB_TOKEN)
    ↓
Structured JSON Output
    ↓
Schema Validation (JsonSchema.Net)
    ↓
Validated Response
```

## Prerequisites

- .NET 9.0 SDK
- GITHUB_TOKEN environment variable (for GitHub Models) or OPENAI_API_KEY

## Usage

### Running the Server

```bash
export GITHUB_TOKEN="your-github-token-here"
dotnet run
```

The server will start and listen on `http://localhost:5000` (or configured port).

### Endpoints

- `POST /agent` - A2A agent endpoint for structured output mapping with dynamic schema
- `GET /health` - Health check endpoint
- `GET /` - Agent information and capabilities

### Example Request

```bash
curl -X POST http://localhost:5000/agent \
  -H "Content-Type: application/json" \
  -d '{
    "input": "{\"fullName\":\"John Doe\",\"yearsOld\":30,\"emailAddress\":\"john@example.com\"}",
    "schema": "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"},\"age\":{\"type\":\"integer\"},\"email\":{\"type\":\"string\"}},\"required\":[\"name\"]}"
  }'
```

**Response** (agent intelligently maps fields):
```json
{
  "name": "John Doe",
  "age": 30,
  "email": "john@example.com"
}
```

## Dynamic Schema Support

The key innovation is accepting the JSON schema dynamically with each request:

```csharp
// 1) Load schema from request at runtime
JsonElement schema = JsonDocument.Parse(request.Schema).RootElement.Clone();

// 2) Configure agent with dynamic schema
var options = new ChatOptions {
    ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, schemaName: "DynamicOutput")
};

// 3) Execute agent
var response = await agent.RunAsync(prompt);

// 4) Parse as dynamic JSON
JsonNode node = JsonNode.Parse(response.Text)!;

// 5) Validate using the same schema
var compiled = JsonSchema.FromText(request.Schema);
var result = compiled.Evaluate(node);
if (!result.IsValid) throw new InvalidOperationException("Schema validation failed");
```

## Fuzzy Logic Mapping

The agent applies intelligent fuzzy logic to map fields:

**Input Field** → **Schema Field**
- `fullName` → `name`
- `yearsOld` → `age`
- `emailAddress` → `email`
- `firstName` + `lastName` → `name`
- `personAge` → `age`

The AI understands semantic relationships and maps appropriately.

## Testing

```bash
dotnet test
```

The test suite includes:
- Health endpoint validation
- Agent information endpoint
- Dynamic schema requirement validation

All tests run without requiring an API key (tests skip agent execution).

## Configuration

### Environment Variables

- `GITHUB_TOKEN` - GitHub personal access token for GitHub Models (preferred)
- `OPENAI_API_KEY` - Alternative API key (fallback)

### GitHub Actions

The agent is configured to work in GitHub Actions runners where `GITHUB_TOKEN` is automatically available, enabling:
- Automated testing
- CI/CD integration
- GitHub-hosted agent deployments

## Agent Configuration

The agent is configured as **DataMappingExpert** with these instructions:

```
You are an expert in data mapping and structured output transformation.
Your task is to intelligently map JSON input to a target schema using fuzzy logic and inference.

Key responsibilities:
- Use intelligent inference to map input fields to schema fields
- Apply fuzzy matching when field names don't exactly match
- Infer appropriate data types based on schema requirements
- Handle nested structures intelligently
- Always produce output that conforms to the schema
```

## Dependencies

- **.NET 9.0**
- **Microsoft.Agents.AI** 1.0.0-preview.251028.1 - Microsoft Agent Framework
- **Microsoft.Agents.AI.Hosting.A2A.AspNetCore** 1.0.0-preview.251028.1 - A2A hosting
- **Azure.AI.Inference** 1.0.0-beta.5 - Azure AI Inference for GitHub Models
- **Microsoft.Extensions.AI.AzureAIInference** 9.10.0-preview.1.25513.3 - Azure AI integration
- **JsonSchema.Net** 7.4.0 - JSON Schema validation
- **System.Text.Json** 9.0.10 - JSON parsing and manipulation

All dependencies are free from known vulnerabilities.

## Key Implementation Details

1. **No Hardcoded Output Types**: The `MappedOutput` class has been removed. Schema is now dynamically provided per request.

2. **Runtime Schema Loading**: Each request provides its own JSON schema string which is parsed and used to configure the agent.

3. **Schema Validation**: Output from the agent is validated against the provided schema using JsonSchema.Net before being returned.

4. **Dynamic Agent Creation**: Agents are created per-request with the specific schema configuration rather than using a singleton agent.

5. **JSON Node Handling**: Uses `JsonNode` for dynamic JSON manipulation instead of strongly-typed deserialization.

## Example Schemas

### Simple Person Schema
```json
{
  "type": "object",
  "properties": {
    "name": { "type": "string" },
    "age": { "type": "integer" },
    "email": { "type": "string" }
  },
  "required": ["name"]
}
```

### Nested Address Schema
```json
{
  "type": "object",
  "properties": {
    "user": {
      "type": "object",
      "properties": {
        "name": { "type": "string" },
        "contact": {
          "type": "object",
          "properties": {
            "email": { "type": "string" },
            "phone": { "type": "string" }
          }
        }
      }
    },
    "tags": {
      "type": "array",
      "items": { "type": "string" }
    }
  }
}
```

## Development

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test --verbosity normal
```

### Auto-Prompt-Optimization

The project includes an auto-prompt-optimization framework to automatically improve agent prompts:

**Run optimization experiment:**
```bash
cd AgenticStructuredOutput.Optimization.CLI
export GITHUB_TOKEN="your-token"
dotnet run
```

**Features:**
- Evaluates prompts across multiple quality dimensions (Relevance, Correctness, Completeness, Grounding)
- Applies mutation strategies (AddExamples, AddConstraints, RephraseInstructions, SimplifyLanguage)
- Uses hill-climbing optimization with adaptive strategy selection
- Tracks optimization history and saves improved prompts

**See detailed documentation:**
- [Optimization Library README](AgenticStructuredOutput.Optimization/README.md)
- [CLI Tool README](AgenticStructuredOutput.Optimization.CLI/README.md)
- [Architecture Decision Records (ADRs)](docs/adr/)

### Running Locally

```bash
dotnet run --project AgenticStructuredOutput/AgenticStructuredOutput.csproj
```

## License

See LICENSE file for details.
