# AgenticStructuredOutput

**A2A (Agent-to-Agent) hosted service using Microsoft Agent Framework** for intelligent JSON data mapping with fuzzy logic and semantic inference, powered by **Azure AI Inference** with GitHub Models. Now with **.NET Aspire orchestration**, **Langfuse integration** for runtime prompt management, and a **Python APO optimizer** for continuous prompt improvement.

## 🚀 What's New: Aspire + Langfuse Integration

This project now includes a complete **.NET Aspire** orchestration setup with:

- **Langfuse Stack**: Full observability and prompt management infrastructure
  - PostgreSQL, ClickHouse, Redis/Valkey, MinIO
  - Langfuse Web UI (port 3000) for prompt versioning
  - Background worker for async processing
- **Python Prompt Optimizer**: FastAPI service using Agent Lightning Framework
  - APO (Automatic Prompt Optimization)
  - Staged rollout strategies (canary → staging → production)
  - REST API for prompt optimization
- **Runtime Prompt Management**: .NET agent fetches prompts from Langfuse
  - No code redeployment for prompt changes
  - Label-based version control (staging, production, etc.)
  - LangfuseClient for REST API integration

### Quick Start with Aspire

```bash
# 1. Build the optimizer image
cd optimizer
docker build -t optimizer-image:latest .

# 2. Run the entire stack
cd ../AppHost
dotnet run

# 3. Access the services
# - Aspire Dashboard: http://localhost:15000 (typically)
# - Langfuse UI: http://localhost:3000
# - Agent API: http://localhost:5000
# - Optimizer API: http://localhost:8000
```

See [AppHost/README.md](AppHost/README.md) for complete documentation.

## Overview

This implementation uses the **Microsoft Agent Framework** with A2A (Agent-to-Agent) hosting to create a web service that intelligently maps JSON input to a target schema. The agent uses **Azure AI Inference** to access GitHub Models with **fuzzy logic** and **inference** for semantic field mapping. The schema is provided dynamically with each request, allowing flexible structured output.


## Key Features

### Core Agent Capabilities
- **Microsoft Agent Framework**: Uses official `Microsoft.Agents.AI` library
- **A2A Hosting**: ASP.NET Core web service with `Microsoft.Agents.AI.Hosting.A2A.AspNetCore`
- **Azure AI Inference**: GitHub Models access via `Azure.AI.Inference`
- **GITHUB_TOKEN Authentication**: Uses GitHub token for model access (works in GitHub Actions)
- **Dynamic JSON Schema**: Accept schema at runtime with each request (no hardcoded output types)
- **Schema Validation**: Uses `JsonSchema.Net` to validate agent output against provided schema
- **AI-Powered Fuzzy Mapping**: Intelligently maps fields like "fullName" → "name"

### Aspire Orchestration & Langfuse
- **.NET Aspire**: Service discovery, health checks, centralized logging
- **Langfuse Integration**: Runtime prompt management without code redeployment
- **Python APO Optimizer**: Automatic prompt optimization with rollout strategies
- **Label-Based Versioning**: Deploy prompts progressively (staging → production)
- **Full Stack**: PostgreSQL, ClickHouse, Redis, MinIO orchestrated by Aspire

## Architecture

### System Topology

```
┌─────────────────────────────────────────────────────────┐
│              .NET Aspire AppHost                        │
│          (Orchestration & Service Discovery)            │
└────────────────────┬────────────────────────────────────┘
                     │
        ┌────────────┼────────────┐
        │            │            │
   ┌────▼────┐  ┌───▼────┐  ┌───▼──────────┐
   │ .NET    │  │ Python │  │   Langfuse   │
   │ Agent   │  │  APO   │  │    Stack     │
   │         │  │Optimizer│  │              │
   └────┬────┘  └───┬────┘  └───┬──────────┘
        │           │            │
        │   Fetch   │   Store    │
        │  Prompts  │  Versions  │
        └───────────┴────────────┘
                    │
          ┌─────────▼─────────┐
          │  Langfuse Web API │
          │ (Prompt Mgmt UI)  │
          └─────────┬─────────┘
                    │
     ┌──────────────┼──────────────┐
     │              │              │
┌────▼────┐  ┌──────▼──────┐  ┌───▼───┐
│Postgres │  │ ClickHouse  │  │ Redis │
│         │  │ (Analytics) │  │(Cache)│
└─────────┘  └─────────────┘  └───────┘
                    │
             ┌──────▼──────┐
             │    MinIO    │
             │ (S3 Store)  │
             └─────────────┘
```

### Agent Data Flow

```
Client Request (JSON + Schema)
    ↓
ASP.NET Core Server (A2A Hosting)
    ↓
Fetch Prompt from Langfuse (by label)
    ↓
AI Agent (Microsoft Agent Framework)
    - Dynamic Schema Configuration
    - ChatResponseFormat.ForJsonSchema()
    - Runtime prompt injection
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
- Docker (for Aspire orchestration)
- GITHUB_TOKEN environment variable (for GitHub Models) or OPENAI_API_KEY

## Usage

### Option 1: Run with Aspire (Recommended)

```bash
# Build optimizer image
cd optimizer
docker build -t optimizer-image:latest .

# Run entire stack with Aspire
cd ../AppHost
dotnet run
```

Access:
- **Aspire Dashboard**: http://localhost:15000 (typically)
- **Langfuse UI**: http://localhost:3000
- **Agent API**: http://localhost:5000
- **Optimizer API**: http://localhost:8000

### Option 2: Run Agent Standalone

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

### Core Agent
- **.NET 9.0**
- **Microsoft.Agents.AI** 1.0.0-preview.251028.1 - Microsoft Agent Framework
- **Microsoft.Agents.AI.Hosting.A2A.AspNetCore** 1.0.0-preview.251028.1 - A2A hosting
- **Azure.AI.Inference** 1.0.0-beta.5 - Azure AI Inference for GitHub Models
- **Microsoft.Extensions.AI.AzureAIInference** 9.10.0-preview.1.25513.3 - Azure AI integration
- **JsonSchema.Net** 7.4.0 - JSON Schema validation
- **System.Text.Json** 9.0.10 - JSON parsing and manipulation

### Aspire Orchestration
- **Aspire.Hosting.AppHost** 9.5.2 - AppHost orchestration
- **Aspire.Hosting.PostgreSQL** 9.5.2 - PostgreSQL hosting
- **Aspire.Hosting.Redis** 9.5.2 - Redis hosting
- **Aspire.ServiceDefaults** 9.5.2 - Service defaults

### Python Optimizer
- **fastapi** 0.115.5 - Web framework
- **uvicorn** 0.32.1 - ASGI server
- **langfuse** 2.56.0 - Langfuse Python SDK
- **agentic-lightning** 0.1.0 - Agent Lightning Framework
- **pydantic** 2.10.3 - Data validation

All dependencies are free from known vulnerabilities.

## Project Structure

```
AgenticStructuredOutput/
├── AgenticStructuredOutput/       # Core .NET agent service
│   ├── Extensions/                # Service registration, builders
│   ├── Services/                  # Agent factory, execution, Langfuse client
│   ├── Resources/                 # Embedded schemas and instructions
│   └── Program.cs                 # Application entry point
├── AgenticStructuredOutput.Tests/ # Test suite
├── AppHost/                       # .NET Aspire orchestration
│   ├── AppHost.cs                 # Service definitions and wiring
│   └── README.md                  # Complete Aspire documentation
├── ServiceDefaults/               # Shared Aspire configuration
├── optimizer/                     # Python APO optimizer
│   ├── optimizer/
│   │   └── api.py                # FastAPI endpoints
│   ├── Dockerfile                # Container definition
│   └── README.md                 # Optimizer documentation
└── README.md                      # This file
```

## Prompt Management Workflow

### 1. Create Initial Prompt (via Langfuse UI)
```bash
# Access Langfuse at http://localhost:3000
# Create project and generate API keys
# Create prompt with label "production"
```

### 2. Optimize Prompt (Python Optimizer)
```bash
curl -X POST http://localhost:8000/optimize \
  -H "Content-Type: application/json" \
  -d '{
    "name": "agent/system-prompt",
    "draft": {
      "type": "text",
      "prompt": "You are a helpful agent for {{domain}}.",
      "config": {"temperature": 0.7}
    },
    "objective": "improve clarity and reduce tokens",
    "labels": ["staging"],
    "rollout": {
      "strategy": "gradual",
      "percentage": 10
    }
  }'
```

### 3. Test with Staging Label
```bash
# Agent automatically fetches "staging" version from Langfuse
curl -X POST http://localhost:5000/agent \
  -H "Content-Type: application/json" \
  -d '{
    "input": "{\"fullName\":\"John Doe\"}",
    "schema": "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}}}"
  }'
```

### 4. Promote to Production
```bash
curl -X POST http://localhost:8000/rollout/agent/system-prompt/promote \
  ?from_label=staging&to_label=production
```

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
# Build entire solution including Aspire AppHost
dotnet build

# Build specific project
dotnet build AgenticStructuredOutput/AgenticStructuredOutput.csproj
```

### Running Tests

```bash
dotnet test --verbosity normal
```

### Running Locally (Standalone Mode)

```bash
# Set required environment variables
export GITHUB_TOKEN="your-token-here"
export LANGFUSE_BASE_URL="http://localhost:3000"  # Optional if using Langfuse
export LANGFUSE_PUBLIC_KEY="pk-lf-..."           # Optional
export LANGFUSE_SECRET_KEY="sk-lf-..."           # Optional

# Run the agent
dotnet run --project AgenticStructuredOutput/AgenticStructuredOutput.csproj
```

### Running with Aspire (Recommended)

```bash
# Build optimizer image first
cd optimizer
docker build -t optimizer-image:latest .

# Run entire stack
cd ../AppHost
dotnet run

# Access Aspire dashboard (typically http://localhost:15000)
# All services will be started and orchestrated automatically
```

## Configuration

### Agent Configuration
- **API Keys**: Set `GITHUB_TOKEN` or `OPENAI_API_KEY` environment variable
- **Langfuse**: Optional `LANGFUSE_BASE_URL`, `LANGFUSE_PUBLIC_KEY`, `LANGFUSE_SECRET_KEY`
- **Logging**: Configure in `appsettings.json`

### Aspire Configuration
- **Secrets**: Update in `AppHost/AppHost.cs` (NEXTAUTH_SECRET, SALT, ENCRYPTION_KEY)
- **Ports**: Modify in `AppHost/AppHost.cs` if defaults conflict
- **Resource Limits**: Configure in Aspire service definitions

## Further Reading

- [AppHost/README.md](AppHost/README.md) - Complete Aspire orchestration guide
- [optimizer/README.md](optimizer/README.md) - Python optimizer documentation
- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Langfuse Documentation](https://langfuse.com/docs)
- [Microsoft Agent Framework](https://github.com/microsoft/agents)
- [Agent Lightning Framework](https://github.com/microsoft/agent-lightning)
- [Azure AI Inference](https://learn.microsoft.com/en-us/azure/ai-services/)

## Contributing

Contributions are welcome! Please ensure:
- All tests pass
- Code follows existing patterns
- Documentation is updated
- Security best practices are followed

## License

See LICENSE file for details.
